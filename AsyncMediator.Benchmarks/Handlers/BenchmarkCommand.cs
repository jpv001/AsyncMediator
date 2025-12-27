namespace AsyncMediator.Benchmarks.Handlers;

public sealed class BenchmarkCommand : ICommand
{
    public int Id { get; init; }
}

public sealed class BenchmarkCommandWithResult : ICommand
{
    public int Value { get; init; }
}

public sealed class BenchmarkResult
{
    public int ProcessedValue { get; init; }
}
