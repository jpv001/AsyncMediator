namespace AsyncMediator.Benchmarks.Handlers;

public sealed class BenchmarkCriteria
{
    public int MinId { get; init; }
    public int MaxId { get; init; }
}

public sealed class BenchmarkQueryResult
{
    public required IReadOnlyList<int> Ids { get; init; }
}

public sealed class BenchmarkQueryHandler : IQuery<BenchmarkCriteria, BenchmarkQueryResult>
{
    public Task<BenchmarkQueryResult> Query(BenchmarkCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BenchmarkQueryResult
        {
            Ids = Enumerable.Range(criteria.MinId, criteria.MaxId - criteria.MinId + 1).ToList()
        });
}

public sealed class BenchmarkPrimitiveQueryHandler : IQuery<int, string>
{
    public Task<string> Query(int criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult($"Result for {criteria}");
}

public sealed class BenchmarkLookupQueryHandler : ILookupQuery<IReadOnlyList<string>>
{
    private static readonly IReadOnlyList<string> CachedResult =
        Enumerable.Range(1, 100).Select(i => $"Item {i}").ToList();

    public Task<IReadOnlyList<string>> Query(CancellationToken cancellationToken = default) => Task.FromResult(CachedResult);
}
