using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentBag<Func<Task>> _deferredEvents = new ConcurrentBag<Func<Task>>();
        //This will allow for the caching of handler types so that they're not constantly created
        private readonly ConcurrentDictionary<Type, Type> _handlerCache = new ConcurrentDictionary<Type, Type>();
        

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
            Func<Task> @event;
            while (_deferredEvents.TryTake(out @event))
            {
                await @event.Invoke().ConfigureAwait(false);
            }
        }

        public async Task<TResult> LoadList<TResult>()
        {
            var genericHandlerType = _handlerCache.GetOrAdd(typeof(TResult), typeof(ILookupQuery<>).MakeGenericType(typeof(TResult)));
            var handler = (ILookupQuery<TResult>)_singleInstanceFactory(genericHandlerType);
            return await handler.Query();
        }

        public async Task<TResult> Query<TCriteria, TResult>(TCriteria criteria)
        {
            var genericHandlerType = _handlerCache.GetOrAdd(typeof(IQuery<TCriteria, TResult>), typeof(IQuery<,>).MakeGenericType(typeof(TCriteria), typeof(TResult)));
            var handler = (IQuery<TCriteria, TResult>)_singleInstanceFactory(genericHandlerType);
            return await handler.Query(criteria);
        }

        private ICommandHandler<TCommand> GetCommandHandler<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var genericHandlerType = _handlerCache.GetOrAdd(command.GetType(), typeof(ICommandHandler<>).MakeGenericType(command.GetType()));
            return (ICommandHandler<TCommand>) _singleInstanceFactory(genericHandlerType);
        }

        private IEnumerable<IEventHandler<TEvent>> GetEventHandlers<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            var genericHandlerType = _handlerCache.GetOrAdd(typeof(TEvent), typeof(IEventHandler<>).MakeGenericType(typeof(TEvent)));
            var handlers = _multiInstanceFactory(genericHandlerType);

            return handlers
                .Cast<IEventHandler<TEvent>>()
                .OrderByExecutionOrder();
        }
    }
}