using System.Linq.Expressions;
using System.Reflection;
using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing reflection-based vs expression-compiled delegate invocation.
/// This validates the performance improvement from using Expression.Compile().
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class DelegateInvocationBenchmark
{
    private Mediator _mediator = null!;
    private BenchmarkEvent _event = null!;

    // Simulated old approach (reflection-based)
    private Func<Mediator, object, CancellationToken, Task> _reflectionDelegate = null!;

    // New approach (expression-compiled)
    private Func<Mediator, object, CancellationToken, Task> _expressionDelegate = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkHandlerRegistry();
        registry.RegisterMultiple<IEventHandler<BenchmarkEvent>>(
        [
            new BenchmarkEventHandler()
        ]);

        _mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        _event = new BenchmarkEvent { Id = 1 };

        // Create both delegate types for comparison
        _reflectionDelegate = CreateReflectionDelegate(typeof(BenchmarkEvent));
        _expressionDelegate = CreateExpressionDelegate(typeof(BenchmarkEvent));
    }

    /// <summary>
    /// OLD approach: Uses MethodInfo.Invoke() - allocates array on each call.
    /// </summary>
    private static Func<Mediator, object, CancellationToken, Task> CreateReflectionDelegate(Type eventType)
    {
        var method = typeof(Mediator)
            .GetMethod("PublishBoxed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType);

        return (mediator, @event, ct) => (Task)method.Invoke(mediator, [@event, ct])!;
    }

    /// <summary>
    /// NEW approach: Expression.Compile() - zero allocation after compilation.
    /// </summary>
    private static Func<Mediator, object, CancellationToken, Task> CreateExpressionDelegate(Type eventType)
    {
        var mediatorParam = Expression.Parameter(typeof(Mediator), "mediator");
        var eventParam = Expression.Parameter(typeof(object), "event");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var method = typeof(Mediator)
            .GetMethod("PublishBoxed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType);

        var call = Expression.Call(
            mediatorParam,
            method,
            Expression.Convert(eventParam, eventType),
            ctParam);

        return Expression.Lambda<Func<Mediator, object, CancellationToken, Task>>(
            call, mediatorParam, eventParam, ctParam).Compile();
    }

    [Benchmark(Baseline = true)]
    public async Task Reflection_SingleInvocation()
    {
        await _reflectionDelegate(_mediator, _event, CancellationToken.None);
    }

    [Benchmark]
    public async Task Expression_SingleInvocation()
    {
        await _expressionDelegate(_mediator, _event, CancellationToken.None);
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task Reflection_BulkInvocation(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _reflectionDelegate(_mediator, _event, CancellationToken.None);
        }
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task Expression_BulkInvocation(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _expressionDelegate(_mediator, _event, CancellationToken.None);
        }
    }

    [Benchmark]
    public void DelegateCreation_Reflection()
    {
        _ = CreateReflectionDelegate(typeof(BenchmarkEvent));
    }

    [Benchmark]
    public void DelegateCreation_Expression()
    {
        _ = CreateExpressionDelegate(typeof(BenchmarkEvent));
    }
}
