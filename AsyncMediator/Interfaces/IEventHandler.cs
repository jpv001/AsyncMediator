using System.Threading.Tasks;

namespace AsyncMediator
{
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
        /// <param name="@event">The event instance.</param>
        /// <returns>An async task.</returns>
        Task Handle(TEvent @event);
    }
}