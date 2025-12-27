using AsyncMediator.Benchmarks.Handlers;
using AsyncMediator.Benchmarks.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace AsyncMediator.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
[WarmupCount(5)]
public class CommandHandlingBenchmark
{
    private IMediator _mediator = null!;
    private BenchmarkCommand _command = null!;
    private BenchmarkCommandWithResult _commandWithResult = null!;

    [GlobalSetup]
    public void Setup()
    {
        var registry = new BenchmarkHandlerRegistry();
        _mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);

        registry.Register<ICommandHandler<BenchmarkCommand>>(new BenchmarkCommandHandler(_mediator));
        registry.Register<ICommandHandler<BenchmarkCommandWithResult>>(new BenchmarkCommandWithResultHandler(_mediator));

        _command = new BenchmarkCommand { Id = 1 };
        _commandWithResult = new BenchmarkCommandWithResult { Value = 42 };
    }

    [Benchmark(Baseline = true)]
    public async Task<ICommandWorkflowResult> Send_SingleCommand()
    {
        return await _mediator.Send(_command);
    }

    [Benchmark]
    public async Task<ICommandWorkflowResult> Send_CommandWithResult()
    {
        return await _mediator.Send(_commandWithResult);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task Send_BatchCommands(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _mediator.Send(_command);
        }
    }

    [Benchmark]
    public async Task Send_ConcurrentCommands_10()
    {
        var tasks = new Task<ICommandWorkflowResult>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = _mediator.Send(_command);
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Send_ConcurrentCommands_100()
    {
        var tasks = new Task<ICommandWorkflowResult>[100];
        for (var i = 0; i < 100; i++)
        {
            tasks[i] = _mediator.Send(_command);
        }
        await Task.WhenAll(tasks);
    }
}
