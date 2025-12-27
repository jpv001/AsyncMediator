using System.Collections.Concurrent;

namespace AsyncMediator.Tests.Infrastructure;

public sealed class TestHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();

    public void AddHandlersForEvent<T>(List<T> handlers) where T : class
    {
        _handlers.AddOrUpdate(
            typeof(T),
            _ => handlers.Cast<object>().ToList(),
            (_, existing) => { existing.AddRange(handlers.Cast<object>()); return existing; });
    }

    public void AddHandlersForCommandOrQuery<T>(T handler) where T : class
    {
        _handlers.AddOrUpdate(
            typeof(T),
            _ => [handler],
            (_, existing) => { existing.Add(handler); return existing; });
    }

    public IEnumerable<IEventHandler<T>> GetHandlersFor<T>() where T : IDomainEvent =>
        _handlers.TryGetValue(typeof(IEventHandler<T>), out var list)
            ? list.Cast<IEventHandler<T>>()
            : [];

    public IEnumerable<object> MultiInstanceFactory(Type type) =>
        _handlers.TryGetValue(type, out var list) ? list : [];

    public object SingleInstanceFactory(Type type) =>
        _handlers.TryGetValue(type, out var list) && list.Count > 0
            ? list[0]
            : Activator.CreateInstance(type)!;
}
