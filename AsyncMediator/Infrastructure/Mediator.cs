using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace AsyncMediator;

/// <summary>
/// Default implementation of <see cref="IMediator"/> that routes commands, queries, and events
/// to their respective handlers resolved from a factory delegate.
/// </summary>
/// <param name="multiInstanceFactory">Factory for resolving multiple handler instances (events).</param>
/// <param name="singleInstanceFactory">Factory for resolving single handler instances (commands, queries).</param>
/// <param name="behaviors">Optional explicit pipeline behaviors typed for specific request types.</param>
/// <param name="behaviorFactory">Optional factory for resolving open generic behaviors from DI containers.</param>
public class Mediator(
    MultiInstanceFactory multiInstanceFactory,
    SingleInstanceFactory singleInstanceFactory,
    IEnumerable<object>? behaviors = null,
    BehaviorFactory? behaviorFactory = null) : IMediator
{
    private static readonly ConcurrentDictionary<Type, Func<Mediator, object, CancellationToken, Task>> PublishDelegateCache = new();

    private readonly ConcurrentQueue<(Type EventType, object Event)> _deferredEvents = new();
    private readonly Factory _factory = new(multiInstanceFactory, singleInstanceFactory);
    private readonly BehaviorPipeline _pipeline = behaviors is null && behaviorFactory is null
        ? BehaviorPipeline.Empty
        : new BehaviorPipeline(behaviors ?? [], behaviorFactory);

    /// <inheritdoc />
    public async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        // Fast path: no behaviors registered
        if (_pipeline.IsEmpty)
            return await GetCommandHandler<TCommand>().Handle(command, cancellationToken).ConfigureAwait(false);

        return await _pipeline.Execute<TCommand, ICommandWorkflowResult>(
            command,
            () => GetCommandHandler<TCommand>().Handle(command, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent =>
        _deferredEvents.Enqueue((typeof(TEvent), @event!));

    /// <inheritdoc />
    public async Task ExecuteDeferredEvents(CancellationToken cancellationToken = default)
    {
        while (_deferredEvents.TryDequeue(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publishDelegate = PublishDelegateCache.GetOrAdd(item.EventType, CreatePublishDelegate);
            await publishDelegate(this, item.Event, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<TResult> LoadList<TResult>(CancellationToken cancellationToken = default)
    {
        var handler = _factory.Create<ILookupQuery<TResult>>();

        // Fast path: no behaviors registered
        if (_pipeline.IsEmpty)
            return await handler.Query(cancellationToken).ConfigureAwait(false);

        // Note: LoadList uses a unit/void type as request, behaviors target the result type
        return await _pipeline.Execute<ILookupQuery<TResult>, TResult>(
            handler,
            () => handler.Query(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> Query<TCriteria, TResult>(TCriteria criteria, CancellationToken cancellationToken = default)
    {
        var handler = _factory.Create<IQuery<TCriteria, TResult>>();

        // Fast path: no behaviors registered
        if (_pipeline.IsEmpty)
            return await handler.Query(criteria, cancellationToken).ConfigureAwait(false);

        return await _pipeline.Execute<TCriteria, TResult>(
            criteria,
            () => handler.Query(criteria, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a compiled delegate for publishing events of the specified type.
    /// Uses Expression trees to compile native IL at cache-population time,
    /// eliminating reflection overhead and array allocations on subsequent invocations.
    /// </summary>
    private static Func<Mediator, object, CancellationToken, Task> CreatePublishDelegate(Type eventType)
    {
        var mediatorParam = Expression.Parameter(typeof(Mediator), "mediator");
        var eventParam = Expression.Parameter(typeof(object), "event");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var method = typeof(Mediator)
            .GetMethod(nameof(PublishBoxed), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType);

        var call = Expression.Call(
            mediatorParam,
            method,
            Expression.Convert(eventParam, eventType),
            ctParam);

        return Expression.Lambda<Func<Mediator, object, CancellationToken, Task>>(
            call, mediatorParam, eventParam, ctParam).Compile();
    }

    // Non-generic publish entry point for cached delegates
    private Task PublishBoxed<TEvent>(object @event, CancellationToken cancellationToken) where TEvent : IDomainEvent =>
        Publish((TEvent)@event, cancellationToken);

    private async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var eventHandler in GetEventHandlers<TEvent>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await eventHandler.Handle(@event, cancellationToken).ConfigureAwait(false);
        }
    }

    private ICommandHandler<TCommand> GetCommandHandler<TCommand>()
        where TCommand : ICommand =>
        _factory.Create<ICommandHandler<TCommand>>();

    private IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>()
        where TEvent : IDomainEvent =>
        _factory.CreateEnumerableOf<IEventHandler<TEvent>>();
}
