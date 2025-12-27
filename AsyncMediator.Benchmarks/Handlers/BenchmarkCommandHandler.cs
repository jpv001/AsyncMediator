namespace AsyncMediator.Benchmarks.Handlers;

public sealed class BenchmarkCommandHandler : CommandHandler<BenchmarkCommand>
{
    public BenchmarkCommandHandler(IMediator mediator) : base(mediator) { }

    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken) =>
        Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
}

public sealed class BenchmarkCommandWithResultHandler : CommandHandler<BenchmarkCommandWithResult>
{
    public BenchmarkCommandWithResultHandler(IMediator mediator) : base(mediator) { }

    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        var result = new CommandWorkflowResult<BenchmarkResult>(new BenchmarkResult
        {
            ProcessedValue = Command.Value * 2
        });
        return Task.FromResult<ICommandWorkflowResult>(result);
    }
}

public sealed class BenchmarkCommandWithEventsHandler : CommandHandler<BenchmarkCommand>
{
    public BenchmarkCommandWithEventsHandler(IMediator mediator) : base(mediator) { }

    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new BenchmarkEvent { Id = Command.Id });
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}
