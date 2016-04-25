using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncMediator
{
    /// <inheritdoc />
    public class Mediator : IMediator
    {
        private readonly ConcurrentBag<Func<Task>> _deferredEvents = new ConcurrentBag<Func<Task>>();
        private readonly Factory _factory;

        public Mediator(MultiInstanceFactory multiInstanceFactory, SingleInstanceFactory singleInstanceFactory)
        {
            _factory = new Factory(multiInstanceFactory, singleInstanceFactory);
        }

        public async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command) 
            where TCommand : ICommand
        {
            return await GetCommandHandler<TCommand>().Handle(command).ConfigureAwait(false);
        }

        public void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            _deferredEvents.Add(async () => await Publish(@event).ConfigureAwait(false));
        }

        public async Task ExecuteDeferredEvents()
        {
            Func<Task> @event;

            while (_deferredEvents.TryTake(out @event))
                await @event.Invoke().ConfigureAwait(false);
        }

        public async Task<TResult> LoadList<TResult>()
        {
            var handler = _factory.Create<ILookupQuery<TResult>>();
            return await handler.Query();
        }

        public async Task<TResult> Query<TCriteria, TResult>(TCriteria criteria)
        {
            var handler = _factory.Create<IQuery<TCriteria, TResult>>();
            return await handler.Query(criteria);
        }

        private async Task Publish<TEvent>(TEvent @event) 
            where TEvent : IDomainEvent
        {
            foreach (var eventHandler in GetEventHandlers<TEvent>())
                await eventHandler.Handle(@event).ConfigureAwait(false);
        }

        private ICommandHandler<TCommand> GetCommandHandler<TCommand>()
            where TCommand : ICommand
        {
            return _factory.Create<ICommandHandler<TCommand>>();
        }

        private IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>() 
            where TEvent : IDomainEvent
        {
            var handlers = _factory.CreateEnumerableOf<IEventHandler<TEvent>>();
            return handlers.OrderByExecutionOrder();
        }
    }
}