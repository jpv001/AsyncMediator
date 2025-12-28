namespace AsyncMediator;

/// <summary>
/// The <see cref="Mediator"/> is responsible for Sending <see cref="ICommand"/>s, Deferring <see cref="IDomainEvent"/>s, Executing Deferred <see cref="IDomainEvent"/>s
/// and finding the right <see cref="IEventHandler{TEvent}"/>, see <see cref="IQuery{TCriteria, TResult}"/>, <see cref="ILookupQuery{TResult}"/> or <see cref="ICommandHandler{TCommand}"/>
/// to process the request.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// The Send Command function is used to send an <see cref="ICommand"/> to a registered
    /// <see cref="ICommandHandler{TCommand}"/> that is retrieved from the <see cref="SingleInstanceFactory"/> without a result type.
    /// </summary>
    /// <typeparam name="TCommand">A <see cref="ICommand"/> that is handled by a <see cref="ICommandHandler{TCommand}"/>.</typeparam>
    /// <param name="command">The <see cref="ICommand"/> that needs to be handled.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A class that implements <see cref="ICommandWorkflowResult"/> that contains any relevant <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/> and a Success flag.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the command type.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<ICommandWorkflowResult> Send<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;

    /// <summary>
    /// The Defer Event function is used to queue <see cref="IDomainEvent"/> to be processed when <see cref="ExecuteDeferredEvents"/> is called.
    /// </summary>
    /// <typeparam name="TEvent">A type that implements the <see cref="IDomainEvent"/> to be fired.</typeparam>
    /// <param name="event">The event data contained within a class that inherits from <see cref="IDomainEvent"/>.</param>
    void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent;

    /// <summary>
    /// Executes all of the deferred events that have been queued using <see cref="DeferEvent{TEvent}"/>.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An awaitable task.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task ExecuteDeferredEvents(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all deferred events from the queue without executing them.
    /// This is called automatically when <see cref="Send{TCommand}"/> throws an exception.
    /// Use this method to manually discard events in failure scenarios.
    /// </summary>
    void ClearDeferredEvents();

    /// <summary>
    /// This is used to load data that does not require criteria for its query.
    /// </summary>
    /// <typeparam name="TResult">The generic type of result required.</typeparam>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An awaitable task that returns the requested data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the query type.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<TResult> LoadList<TResult>(CancellationToken cancellationToken = default);

    /// <summary>
    /// This is used to pull data from a data source using a given criteria object and a desired result, the mapping of the request <see cref="Query{TCriteria, TResult}(TCriteria, CancellationToken)"/>
    /// to the correct <see cref="IQuery{TCriteria, TResult}"/> handler is resolved using the <see cref="SingleInstanceFactory"/>.
    /// </summary>
    /// <typeparam name="TCriteria">The type of criteria to be passed in.</typeparam>
    /// <typeparam name="TResult">The desired resulting object.</typeparam>
    /// <param name="criteria">A criteria object that is used to query the data source.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An awaitable task the returns the desired <typeparamref name="TResult"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the query type.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<TResult> Query<TCriteria, TResult>(TCriteria criteria, CancellationToken cancellationToken = default);
}
