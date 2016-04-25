using System;
using System.Collections.Generic;

namespace AsyncMediator.Interfaces
{
    public interface IFactory
    {
        T Create<T>() where T : class;

        IEnumerable<T> CreateEnumerableOf<T>() where T : class;

        bool TryCreate<T>(out T result) where T : class;

        bool TryCreateEnumerableOf<T>(out IEnumerable<T> result) where T : class;

        object Create(Type type);

        IEnumerable<object> CreateEnumerableOf(Type type);

        bool TryCreate(Type type, out object result);

        bool TryCreateEnumerableOf(Type type, out IEnumerable<object> result);
    }
}