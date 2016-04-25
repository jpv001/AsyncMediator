using System;
using System.Collections.Generic;
using System.Linq;
using AsyncMediator.Interfaces;

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
            this._multiInstanceFactory = multiInstanceFactory;
            this._singleInstanceFactory = singleInstanceFactory;
        }

        public T Create<T>() where T : class
        {
            return (T) this.Create(typeof(T));
        }

        public IEnumerable<T> CreateEnumerableOf<T>() where T : class
        {
            return this.CreateEnumerableOf(typeof(T)).Cast<T>();
        }

        public bool TryCreate<T>(out T result) where T : class
        {
            object innerResult;
            bool created = this.TryCreate(typeof(T), out innerResult);

            result = (T) innerResult;

            return created;
        }

        public bool TryCreateEnumerableOf<T>(out IEnumerable<T> result) where T : class
        {
            IEnumerable<object> innerResult;
            bool created = this.TryCreateEnumerableOf(typeof(T), out innerResult);

            result = innerResult.Cast<T>();

            return created;
        }

        public object Create(Type type)
        {
            return this._singleInstanceFactory.Invoke(type);
        }

        public IEnumerable<object> CreateEnumerableOf(Type type)
        {
            return this._multiInstanceFactory.Invoke(type);
        }

        public bool TryCreate(Type type, out object result)
        {
            bool retrieved = false;

            try
            {
                result = this._singleInstanceFactory.Invoke(type);
                retrieved = true;
            }
            catch (Exception)
            {
                result = null;
            }

            return retrieved;
        }

        public bool TryCreateEnumerableOf(Type type, out IEnumerable<object> result)
        {
            bool retrieved = false;

            try
            {
                result = this._multiInstanceFactory.Invoke(type);
                retrieved = true;
            }
            catch (Exception)
            {
                result = null;
            }

            return retrieved;
        }
    }
}