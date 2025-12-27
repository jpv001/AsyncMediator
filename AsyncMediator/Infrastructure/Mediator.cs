using System.Collections.Concurrent;
using System.Reflection;

namespace AsyncMediator;

/// <summary>
/// Default implementation of <see cref="IMediator"/> that routes commands, queries, and events
/// to their respective handlers resolved from a factory delegate.
/// </summary>
/// <param name="multiInstanceFactory">Factory for resolving multiple handler instances (events).</param>
/// <param name="singleInstanceFactory">Factory for resolving single handler instances (commands, queries).</param>
public class Mediator(MultiInstanceFactory multiInstanceFactory, SingleInstanceFactory singleInstanceFactory) : IMediator
{
    private static readonly ConcurrentDictionary<Type, Func<Mediator, object, CancellationToken, Task>> PublishDelegateCache = new();

    private readonly ConcurrentQueue<(Type EventType, object Event)> _deferredEvents = new();
    private readonly Factory _factory = new(multiInstanceFactory, singleInstanceFactory);

    /// <inheritdoc />
    public async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand =>
        await GetCommandHandler<TCommand>().Handle(command, cancellationToken).ConfigureAwait(false);

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
        return await handler.Query(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> Query<TCriteria, TResult>(TCriteria criteria, CancellationToken cancellationToken = default)
    {
        var handler = _factory.Create<IQuery<TCriteria, TResult>>();
        return await handler.Query(criteria, cancellationToken).ConfigureAwait(false);
    }

    private static Func<Mediator, object, CancellationToken, Task> CreatePublishDelegate(Type eventType)
    {
        var method = typeof(Mediator)
            .GetMethod(nameof(PublishBoxed), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType);

        return (mediator, @event, ct) => (Task)method.Invoke(mediator, [@event, ct])!;
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
