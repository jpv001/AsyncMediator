using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class MemoryAllocationBenchmark
{
    private IMediator _mediator = null!;
    private IMediator _mediatorWithEvents = null!;
    private BenchmarkCommand _command = null!;
    private BenchmarkEvent _event = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkHandlerRegistry();
        _mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        registry.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediator));
        registry.Register<ICommandHandler<BenchmarkCommandWithResult>>(new BenchmarkCommandWithResultHandler(_mediator));

        var registryWithEvents = new BenchmarkHandlerRegistry();
        _mediatorWithEvents = new Mediator(registryWithEvents.MultiInstanceFactory, registryWithEvents.SingleInstanceFactory);
        registryWithEvents.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandWithEventsHandler(_mediatorWithEvents));
        registryWithEvents.RegisterMultiple<IEventHandler<BenchmarkEvent>>(
        [
            new BenchmarkEventHandler()
        ]);

        _command = new BenchmarkCommand { Id = 1 };
        _event = new BenchmarkEvent { Id = 1 };
    }

    [Benchmark(Baseline = true)]
    public async Task<ICommandWorkflowResult> Command_Lifecycle_Simple()
    {
        return await _mediator.Send(_command);
    }

    [Benchmark]
    public async Task<ICommandWorkflowResult> Command_Lifecycle_WithEvents()
    {
        return await _mediatorWithEvents.Send(_command);
    }

    [Benchmark]
    public void EventQueue_Growth_100Events()
    {
        for (var i = 0; i < 100; i++)
        {
            _mediator.DeferEvent(_event);
        }
    }

    [Benchmark]
    public void EventQueue_Growth_1000Events()
    {
        for (var i = 0; i < 1000; i++)
        {
            _mediator.DeferEvent(_event);
        }
    }

    [Benchmark]
    public void ValidationContext_Creation()
    {
        _ = new ValidationContext();
    }

    [Benchmark]
    public void CommandWorkflowResult_Creation()
    {
        _ = CommandWorkflowResult.Ok();
    }

    [Benchmark]
    public void CommandWorkflowResult_WithError()
    {
        _ = CommandWorkflowResult.WithError("Field", "Error message");
    }

    [Benchmark]
    public void Mediator_Instantiation()
    {
        var registry = new BenchmarkHandlerRegistry();
        _ = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
    }
}
