using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator
{
    /// <inheritdoc/>
    public class Mediator : IMediator
    {
        private readonly MultiInstanceFactory _multiInstanceFactory;
        private readonly SingleInstanceFactory _singleInstanceFactory;

        private readonly List<Func<Task>> _deferredEvents = new List<Func<Task>>();
        private readonly List<Func<Task>> _queuedEvents = new List<Func<Task>>();

        public Mediator(MultiInstanceFactory multiInstanceFactory, SingleInstanceFactory singleInstanceFactory)
        {
            _multiInstanceFactory = multiInstanceFactory;
            _singleInstanceFactory = singleInstanceFactory;
        }

        private async Task Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            foreach (var h in GetEventHandlers(@event))
            {
                await h.Handle(@event).ConfigureAwait(false);
            }
        }

        public async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command) where TCommand : ICommand
        {
            return await GetCommandHandler(command).Handle(command).ConfigureAwait(false);
        }

        public void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            _deferredEvents.Add(async () => await Publish(@event).ConfigureAwait(false));
        }

        public async Task ExecuteDeferredEvents()
        {
            while (_deferredEvents.Any())
            {
                _queuedEvents.AddRange(_deferredEvents);

                _deferredEvents.Clear();

                foreach (var @event in _queuedEvents)
                {
                    await @event.Invoke().ConfigureAwait(false);
                }

                _queuedEvents.Clear();
            }
        }

        public async Task<TResult> LoadList<TResult>()
        {
            var queryHandler = typeof(ILookupQuery<>).MakeGenericType(typeof(TResult));
            var handler = (ILookupQuery<TResult>)_singleInstanceFactory(queryHandler);
            return await handler.Query();
        }

        public async Task<TResult> Query<TCriteria, TResult>(TCriteria criteria)
        {
            var queryHandler = typeof(IQuery<,>).MakeGenericType(typeof(TCriteria), typeof(TResult));
            var handler = (IQuery<TCriteria, TResult>)_singleInstanceFactory(queryHandler);
            return await handler.Query(criteria);
        }

        private ICommandHandler<TCommand> GetCommandHandler<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            return (ICommandHandler<TCommand>)_singleInstanceFactory(handlerType);
        }

        private IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            var handlerType = typeof(IEventHandler<>).MakeGenericType(@event.GetType());
            var handlers = _multiInstanceFactory(handlerType);

            return handlers
                .Cast<IEventHandler<TEvent>>()
                .OrderByExecutionOrder();
        }
    }
}