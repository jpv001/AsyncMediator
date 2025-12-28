using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace AsyncMediator;

/// <summary>
/// Factory delegate for resolving pipeline behaviors from a DI container.
/// Returns behaviors implementing <c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c> for the requested type.
/// </summary>
/// <param name="behaviorType">The closed generic type <c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c> to resolve.</param>
/// <returns>Enumerable of behavior instances.</returns>
public delegate IEnumerable<object> BehaviorFactory(Type behaviorType);

/// <summary>
/// Internal pipeline executor that chains behaviors around handler execution.
/// Uses FrozenDictionary for O(1) behavior lookup with zero-allocation on hot paths.
/// Supports both explicit behavior instances and DI factory resolution for open generics.
/// </summary>
internal sealed class BehaviorPipeline
{
    /// <summary>
    /// Singleton empty pipeline for zero-overhead when no behaviors are registered.
    /// </summary>
    public static readonly BehaviorPipeline Empty = new([], null);

    private readonly FrozenDictionary<(Type Request, Type Response), object[]> _explicitBehaviors;
    private readonly BehaviorFactory? _behaviorFactory;
    private readonly ConcurrentDictionary<(Type Request, Type Response), object[]> _resolvedBehaviors = new();

    /// <summary>
    /// Gets whether this pipeline has no behaviors registered and no factory.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Creates a new behavior pipeline with explicit behaviors and optional factory.
    /// </summary>
    /// <param name="behaviors">Explicit behavior instances (typed for specific request types).</param>
    /// <param name="behaviorFactory">Optional factory for resolving open generic behaviors from DI.</param>
    public BehaviorPipeline(IEnumerable<object> behaviors, BehaviorFactory? behaviorFactory)
    {
        _behaviorFactory = behaviorFactory;
        var behaviorList = behaviors.ToList();

        IsEmpty = behaviorList.Count == 0 && behaviorFactory is null;

        _explicitBehaviors = behaviorList.Count == 0
            ? FrozenDictionary<(Type, Type), object[]>.Empty
            : BuildBehaviorMap(behaviorList);
    }

    /// <summary>
    /// Executes the pipeline for the given request type.
    /// Fast path: when no behaviors exist for this type, directly invokes the handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> Execute<TRequest, TResponse>(
        TRequest request,
        RequestHandlerDelegate<TResponse> handler,
        CancellationToken cancellationToken)
    {
        var behaviors = GetBehaviorsForRequest<TRequest, TResponse>();

        // Fast path: no behaviors for this request type
        if (behaviors.Length == 0)
            return handler();

        return ExecutePipeline(request, behaviors, handler, cancellationToken);
    }

    /// <summary>
    /// Gets behaviors for the request and response types, combining explicit and factory-resolved behaviors.
    /// Results are cached per request/response pair for optimal performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object[] GetBehaviorsForRequest<TRequest, TResponse>()
    {
        // Fast path: no factory, use explicit behaviors only
        if (_behaviorFactory is null)
            return _explicitBehaviors.TryGetValue((typeof(TRequest), typeof(TResponse)), out var b) ? b : [];

        // Cache resolved behaviors per request/response type pair
        return _resolvedBehaviors.GetOrAdd((typeof(TRequest), typeof(TResponse)), _ =>
        {
            var explicitList = _explicitBehaviors.TryGetValue((typeof(TRequest), typeof(TResponse)), out var e) ? e : [];
            var factoryList = _behaviorFactory(typeof(IPipelineBehavior<TRequest, TResponse>)).ToArray();

            if (explicitList.Length == 0 && factoryList.Length == 0)
                return [];

            if (explicitList.Length == 0)
                return factoryList;

            if (factoryList.Length == 0)
                return explicitList;

            // Combine explicit (first) and factory behaviors
            var combined = new object[explicitList.Length + factoryList.Length];
            explicitList.CopyTo(combined, 0);
            factoryList.CopyTo(combined, explicitList.Length);
            return combined;
        });
    }

    /// <summary>
    /// Builds the behavior chain and executes it.
    /// </summary>
    private static async Task<TResponse> ExecutePipeline<TRequest, TResponse>(
        TRequest request,
        object[] behaviors,
        RequestHandlerDelegate<TResponse> handler,
        CancellationToken cancellationToken)
    {
        // Build pipeline from innermost (handler) to outermost (first behavior)
        var current = handler;

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = (IPipelineBehavior<TRequest, TResponse>)behaviors[i];
            var next = current;
            current = () => behavior.Handle(request, next, cancellationToken);
        }

        return await current().ConfigureAwait(false);
    }

    /// <summary>
    /// Groups behaviors by their request and response types for efficient lookup.
    /// </summary>
    private static FrozenDictionary<(Type Request, Type Response), object[]> BuildBehaviorMap(List<object> behaviors)
    {
        var map = new Dictionary<(Type Request, Type Response), List<object>>();

        foreach (var behavior in behaviors)
        {
            var behaviorType = behavior.GetType();

            // Find all IPipelineBehavior<TRequest, TResponse> interfaces
            foreach (var iface in behaviorType.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                var genericDef = iface.GetGenericTypeDefinition();
                if (genericDef != typeof(IPipelineBehavior<,>))
                    continue;

                // Get both TRequest and TResponse types
                var genericArgs = iface.GetGenericArguments();
                var key = (Request: genericArgs[0], Response: genericArgs[1]);

                if (!map.TryGetValue(key, out var list))
                {
                    list = [];
                    map[key] = list;
                }

                list.Add(behavior);
            }
        }

        // Convert to frozen dictionary with arrays for optimal performance
        return map.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray());
    }
}
