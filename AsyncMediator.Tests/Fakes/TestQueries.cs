namespace AsyncMediator.Tests.Fakes;

public sealed class FakeRangeCriteria
{
    public int MaxValue { get; set; }
    public int MinValue { get; set; }
}

public sealed class SingleIdCriteria
{
    public int Id { get; set; }
}

public sealed class SingleNameCriteria
{
    public string Name { get; set; } = string.Empty;
}

public sealed class FakeResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelatedData { get; set; } = string.Empty;
}

public static class FakeDataStore
{
    public static readonly List<FakeResult> Results = Enumerable
        .Range(1, 10)
        .Select(i => new FakeResult
        {
            Id = i,
            Name = $"Name{i}",
            RelatedData = $"RelatedData{i}"
        })
        .ToList();
}

public sealed class FindFakeResultByRangeCriteria : IQuery<FakeRangeCriteria, List<FakeResult>>
{
    public Task<List<FakeResult>> Query(FakeRangeCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results
            .Where(x => x.Id <= criteria.MaxValue && x.Id >= criteria.MinValue)
            .ToList());
}

public sealed class FindFakeResultByPrimitiveType : IQuery<int, List<FakeResult>>
{
    public Task<List<FakeResult>> Query(int criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results.Where(x => x.Id == criteria).ToList());
}

public sealed class FindPrimitiveTypeByCriteria : IQuery<SingleNameCriteria, int>
{
    public Task<int> Query(SingleNameCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results.Single(x => x.Name == criteria.Name).Id);
}

public sealed class MultipleQueryTypesInOneObject : IQuery<SingleIdCriteria, FakeResult>, IQuery<SingleNameCriteria, FakeResult>
{
    public Task<FakeResult> Query(SingleIdCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results.SingleOrDefault(x => x.Id == criteria.Id)!);

    public Task<FakeResult> Query(SingleNameCriteria criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results.SingleOrDefault(x => x.Name == criteria.Name)!);
}

public sealed class FindResultForLookup : ILookupQuery<List<FakeResult>>
{
    public Task<List<FakeResult>> Query(CancellationToken cancellationToken = default) =>
        Task.FromResult(FakeDataStore.Results.ToList());
}
