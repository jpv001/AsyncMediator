using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class HandlerResolutionBenchmark
{
    private BenchmarkHandlerRegistry _registry = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registry = new BenchmarkHandlerRegistry();
        var tempMediator = new Mediator(_registry.MultiInstanceFactory, _registry.SingleInstanceFactory);

        _registry.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(tempMediator));
        _registry.Register<ICommandHandler<BenchmarkCommandWithResult>>(new BenchmarkCommandWithResultHandler(tempMediator));
        _registry.Register<IQuery<BenchmarkCriteria, BenchmarkQueryResult>>(new BenchmarkQueryHandler());
        _registry.Register<IQuery<int, string>>(new BenchmarkPrimitiveQueryHandler());
        _registry.Register<ILookupQuery<IReadOnlyList<string>>>(new BenchmarkLookupQueryHandler());
        _registry.RegisterMultiple<IEventHandler<BenchmarkEvent>>(
        [
            new BenchmarkEventHandler(),
            new SlowBenchmarkEventHandler()
        ]);
    }

    [Benchmark(Baseline = true)]
    public object Resolve_SingleCommandHandler()
    {
        return _registry.SingleInstanceFactory(typeof(ICommandHandler<BenchmarkCommand>));
    }

    [Benchmark]
    public object Resolve_QueryHandler()
    {
        return _registry.SingleInstanceFactory(typeof(IQuery<BenchmarkCriteria, BenchmarkQueryResult>));
    }

    [Benchmark]
    public object[] Resolve_MultipleEventHandlers()
    {
        return _registry.MultiInstanceFactory(typeof(IEventHandler<BenchmarkEvent>)).ToArray();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public void Resolve_BatchResolutions(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _ = _registry.SingleInstanceFactory(typeof(ICommandHandler<BenchmarkCommand>));
            _ = _registry.MultiInstanceFactory(typeof(IEventHandler<BenchmarkEvent>));
        }
    }

    [Benchmark]
    public void Resolve_ColdPath_NewMediator()
    {
        var registry = new BenchmarkHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        registry.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(mediator));
    }
}
