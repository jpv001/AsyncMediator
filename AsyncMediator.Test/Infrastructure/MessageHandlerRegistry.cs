using System;
using System.Collections.Generic;
using System.Linq;
using AsyncMediator;

namespace AsyncMediator.Test
{
    public class MessageHandlerRegistry
    {
        private readonly IDictionary<Type, List<object>> _eventHandlers = new Dictionary<Type, List<object>>();
        public Func<SingleInstanceFactory> SingleInstanceDelegate;
        public Func<MultiInstanceFactory> MultiInstanceDelegate;

        public MessageHandlerRegistry()
        {
            MultiInstanceDelegate = () => MultiInstanceFactory;
            SingleInstanceDelegate = () => SingleInstanceFactory;
        }

        public void AddHandlersForEvent<T>(List<T> handlers)
        {
            _eventHandlers.Add(typeof(T), handlers.Cast<object>().ToList());
        }

        public void AddHandlersForCommandOrQuery<T>(T handler)
        {
            _eventHandlers.Add(typeof(T), new List<object> { handler });
        }

        public IEnumerable<IEventHandler<T>> GetHandlersFor<T>() where T : IDomainEvent
        {
            if (!_eventHandlers.ContainsKey(typeof(IEventHandler<T>))) return new List<IEventHandler<T>>();
            return _eventHandlers[typeof(IEventHandler<T>)].Cast<IEventHandler<T>>();
        }

        public IEnumerable<object> MultiInstanceFactory(Type type)
        {
            return _eventHandlers.ContainsKey(type) ? _eventHandlers[type] : new List<object>();
        }

        public object SingleInstanceFactory(Type type)
        {
            return _eventHandlers.ContainsKey(type) ? _eventHandlers[type].FirstOrDefault() : Activator.CreateInstance(type);
        }
    }
}
