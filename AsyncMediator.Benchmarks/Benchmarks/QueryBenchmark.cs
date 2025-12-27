using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class QueryBenchmark
{
    private IMediator _mediator = null!;
    private BenchmarkCriteria _criteria = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkHandlerRegistry();
        registry.Register<IQuery<BenchmarkCriteria, BenchmarkQueryResult>>(new BenchmarkQueryHandler());
        registry.Register<IQuery<int, string>>(new BenchmarkPrimitiveQueryHandler());
        registry.Register<ILookupQuery<IReadOnlyList<string>>>(new BenchmarkLookupQueryHandler());

        _mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        _criteria = new BenchmarkCriteria { MinId = 1, MaxId = 100 };
    }

    [Benchmark(Baseline = true)]
    public async Task<BenchmarkQueryResult> Query_ComplexCriteria()
    {
        return await _mediator.Query<BenchmarkCriteria, BenchmarkQueryResult>(_criteria);
    }

    [Benchmark]
    public async Task<string> Query_PrimitiveCriteria()
    {
        return await _mediator.Query<int, string>(42);
    }

    [Benchmark]
    public async Task<IReadOnlyList<string>> LoadList()
    {
        return await _mediator.LoadList<IReadOnlyList<string>>();
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    public async Task Query_BatchQueries(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _mediator.Query<BenchmarkCriteria, BenchmarkQueryResult>(_criteria);
        }
    }

    [Benchmark]
    public async Task Query_ConcurrentQueries_10()
    {
        var tasks = new Task<BenchmarkQueryResult>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = _mediator.Query<BenchmarkCriteria, BenchmarkQueryResult>(_criteria);
        }
        await Task.WhenAll(tasks);
    }
}
