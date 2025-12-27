namespace AsyncMediator.Benchmarks.Handlers;

public sealed class BenchmarkEventHandler : IEventHandler<BenchmarkEvent>
{
    private int _processedCount;

    public int ProcessedCount => _processedCount;

    public Task Handle(BenchmarkEvent @event, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _processedCount);
        return Task.CompletedTask;
    }
}

public sealed class SlowBenchmarkEventHandler : IEventHandler<BenchmarkEvent>
{
    public Task Handle(BenchmarkEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
