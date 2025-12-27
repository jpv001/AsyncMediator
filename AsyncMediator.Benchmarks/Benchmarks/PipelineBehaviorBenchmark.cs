using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks verifying zero-cost opt-in for pipeline behaviors.
/// Critical: Mediator with empty behaviors should have negligible overhead vs no behaviors.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class PipelineBehaviorBenchmark
{
    private IMediator _mediatorNoBehaviors = null!;
    private IMediator _mediatorEmptyBehaviors = null!;
    private IMediator _mediatorOneBehavior = null!;
    private IMediator _mediatorThreeBehaviors = null!;
    private BenchmarkCommand _command = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup for no behaviors baseline
        var registry1 = new BenchmarkHandlerRegistry();
        _mediatorNoBehaviors = new Mediator(registry1.MultiInstanceFactory, registry1.SingleInstanceFactory);
        registry1.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediatorNoBehaviors));

        // Setup for empty behaviors (should match baseline)
        var registry2 = new BenchmarkHandlerRegistry();
        _mediatorEmptyBehaviors = new Mediator(registry2.MultiInstanceFactory, registry2.SingleInstanceFactory, behaviors: []);
        registry2.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediatorEmptyBehaviors));

        // Setup for 1 behavior
        var registry3 = new BenchmarkHandlerRegistry();
        _mediatorOneBehavior = new Mediator(
            registry3.MultiInstanceFactory,
            registry3.SingleInstanceFactory,
            behaviors: [new NoOpBehavior<BenchmarkCommand, ICommandWorkflowResult>()]);
        registry3.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediatorOneBehavior));

        // Setup for 3 behaviors
        var registry4 = new BenchmarkHandlerRegistry();
        _mediatorThreeBehaviors = new Mediator(
            registry4.MultiInstanceFactory,
            registry4.SingleInstanceFactory,
            behaviors:
            [
                new NoOpBehavior<BenchmarkCommand, ICommandWorkflowResult>(),
                new NoOpBehavior<BenchmarkCommand, ICommandWorkflowResult>(),
                new NoOpBehavior<BenchmarkCommand, ICommandWorkflowResult>()
            ]);
        registry4.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediatorThreeBehaviors));

        _command = new BenchmarkCommand { Id = 1 };
    }

    [Benchmark(Baseline = true)]
    public Task<ICommandWorkflowResult> Mediator_NoBehaviors()
        => _mediatorNoBehaviors.Send(_command);

    [Benchmark]
    public Task<ICommandWorkflowResult> Mediator_EmptyBehaviors()
        => _mediatorEmptyBehaviors.Send(_command);

    [Benchmark]
    public Task<ICommandWorkflowResult> Mediator_OneBehavior()
        => _mediatorOneBehavior.Send(_command);

    [Benchmark]
    public Task<ICommandWorkflowResult> Mediator_ThreeBehaviors()
        => _mediatorThreeBehaviors.Send(_command);

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task Send_Batch_NoBehaviors(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _mediatorNoBehaviors.Send(_command);
        }
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task Send_Batch_OneBehavior(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _mediatorOneBehavior.Send(_command);
        }
    }
}

/// <summary>
/// Minimal no-op behavior for benchmarking pure overhead.
/// </summary>
file sealed class NoOpBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next();
}
