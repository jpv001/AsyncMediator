using System.Threading.Tasks;

namespace AsyncMediator
{
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
        // <typeparam name="TCommand">A <see cref="ICommand"/> that is handled by a <see cref="ICommandHandler{TCommand}"/>.</typeparam>
        /// <param name="command">The <see cref="ICommand"/> that needs to be handled.</param>
        /// <returns>A class that implements <see cref="ICommandWorkflowResult"/> that contains any relevant <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/> and a Success flag.</returns>
        Task<ICommandWorkflowResult> Send<TCommand>(TCommand command) where TCommand : ICommand;

        /// <summary>
        /// The Defer Event function is used to queue <see cref="IDomainEvent"/> to be processed when <see cref="ExecuteDeferredEvents"/> is called.
        /// </summary>
        /// <typeparam name="TEvent">A type that implements the <see cref="IDomainEvent"/> to be fired.</typeparam>
        /// <param name="event">The event data contained within a class that inherits from <see cref="IDomainEvent"/>.</param>
        void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent;

        /// <summary>
        /// Executes all of the deferred events that have been queued using <see cref="DeferEvent{TEvent}"/>.
        /// </summary>
        /// <returns>An awaitable task.</returns>
        Task ExecuteDeferredEvents();

        /// <summary>
        /// This is used to load data that does not require criteria for its query.
        /// </summary>
        /// <typeparam name="TResult">The generic type of result required.</typeparam>
        /// <returns>An awaitable task that returns the requested data.</returns>
        Task<TResult> LoadList<TResult>();

        /// <summary>
        /// This is used to pull data from a data source using a given criteria object and a desired result, the mapping of the request <see cref="Query{TCriteria, TResult}(TCriteria)"/>
        /// to the correct <see cref="IQuery{TCriteria, TResult}"/> handler is resolved using the <see cref="SingleInstanceFactory"/>.
        /// </summary>
        /// <typeparam name="TCriteria">The type of criteria to be passed in.</typeparam>
        /// <typeparam name="TResult">The desired resulting object.</typeparam>
        /// <param name="criteria">A criteria object that is used to query the data source.</param>
        /// <returns>An awaitable task the returns the desired <see cref="TResult"/>.</returns>
        Task<TResult> Query<TCriteria, TResult>(TCriteria criteria);
    }
}