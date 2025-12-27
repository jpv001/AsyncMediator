namespace AsyncMediator.Benchmarks.Handlers;

public sealed class BenchmarkEvent : IDomainEvent
{
    public int Id { get; init; }
}
