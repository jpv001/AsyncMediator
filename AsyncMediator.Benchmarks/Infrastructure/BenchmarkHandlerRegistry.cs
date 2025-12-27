using System.Collections.Concurrent;

namespace AsyncMediator.Benchmarks.Infrastructure;

public sealed class BenchmarkHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();

    public void Register<T>(T handler) where T : class
    {
        _handlers.AddOrUpdate(
            typeof(T),
            _ => [handler],
            (_, list) => { list.Add(handler); return list; });
    }

    public void RegisterMultiple<T>(IEnumerable<T> handlers) where T : class
    {
        var handlerList = handlers.Cast<object>().ToList();
        _handlers.AddOrUpdate(
            typeof(T),
            _ => handlerList,
            (_, list) => { list.AddRange(handlerList); return list; });
    }

    public IEnumerable<object> MultiInstanceFactory(Type type) =>
        _handlers.TryGetValue(type, out var list) ? list : [];

    public object SingleInstanceFactory(Type type) =>
        _handlers.TryGetValue(type, out var list) && list.Count > 0
            ? list[0]
            : throw new InvalidOperationException($"No handler registered for {type.Name}");
}
