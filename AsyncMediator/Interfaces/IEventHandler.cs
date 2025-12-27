namespace AsyncMediator;

/// <summary>
/// Inherit this interface to handle a <see cref="IDomainEvent"/>.
/// You may have multiple Event Handlers for a single <see cref="IDomainEvent"/>.
/// </summary>
/// <typeparam name="TEvent">A class that implements a <see cref="IDomainEvent"/>.</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="event">The event instance.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An async task.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task Handle(TEvent @event, CancellationToken cancellationToken = default);
}
