using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class EventDeferralBenchmark
{
    private IMediator _mediator = null!;
    private BenchmarkEvent _event = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkHandlerRegistry();
        registry.RegisterMultiple<IEventHandler<BenchmarkEvent>>(
        [
            new BenchmarkEventHandler(),
            new SlowBenchmarkEventHandler()
        ]);

        _mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        _event = new BenchmarkEvent { Id = 1 };
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _mediator.ExecuteDeferredEvents().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public void DeferEvent_Single()
    {
        _mediator.DeferEvent(_event);
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public void DeferEvent_Bulk(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _mediator.DeferEvent(_event);
        }
    }

    [Benchmark]
    public async Task DeferAndExecute_Single()
    {
        _mediator.DeferEvent(_event);
        await _mediator.ExecuteDeferredEvents();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task DeferAndExecute_Bulk(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _mediator.DeferEvent(_event);
        }
        await _mediator.ExecuteDeferredEvents();
    }

    [Benchmark]
    public void DeferEvent_ConcurrentThreads_10()
    {
        Parallel.For(0, 10, _ =>
        {
            for (var i = 0; i < 1000; i++)
            {
                _mediator.DeferEvent(_event);
            }
        });
    }
}
