using System;
using System.Collections.Generic;
using System.Linq;

namespace AsyncMediator
{
    /// <summary>
    ///     A delegate for creating a single instance of a service type.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The service instance.</returns>
    public delegate object SingleInstanceFactory(Type serviceType);

    /// <summary>
    ///     A delegate for creating multiple instances of a service type.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>A list of service instances.</returns>
    public delegate IEnumerable<object> MultiInstanceFactory(Type serviceType);

    internal class Factory : IFactory
    {
        private readonly MultiInstanceFactory _multiInstanceFactory;
        private readonly SingleInstanceFactory _singleInstanceFactory;

        public Factory(MultiInstanceFactory multiInstanceFactory, SingleInstanceFactory singleInstanceFactory)
        {
            _multiInstanceFactory = multiInstanceFactory;
            _singleInstanceFactory = singleInstanceFactory;
        }

        public IEnumerable<T> CreateEnumerableOf<T>() where T : class
        {
            return _multiInstanceFactory(typeof(T)).Cast<T>();
        }

        public T Create<T>() where T : class
        {
            return (T) _singleInstanceFactory(typeof(T));
        }
    }
}