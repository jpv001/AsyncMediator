namespace AsyncMediator.Tests.Fakes;

public sealed class TestCommandHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (Command.Id == 999)
            validationContext.AddError("UserId", "Validation Failed");
        return Task.CompletedTask;
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent());
        Mediator.DeferEvent(new FakeEventFromHandler());
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

public sealed class TestCommandWithResultHandler(IMediator mediator) : CommandHandler<TestCommandWithResult>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (Command.Id == 999)
            validationContext.AddError("UserId", "Validation Failed");
        return Task.CompletedTask;
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent());
        Mediator.DeferEvent(new FakeEventFromHandler());
        var result = new CommandWorkflowResult<TestCommandResult>(new TestCommandResult { ResultingValue = 5 });
        return Task.FromResult<ICommandWorkflowResult>(result);
    }
}

public sealed class TestMultipleCommandHandlerWithResult(IMediator mediator) : CommandHandler<TestMultipleCommandWithResult>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (string.Equals(Command.Name, "foo", StringComparison.OrdinalIgnoreCase))
            validationContext.AddError("UserId", "Validation Failed");
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        var commandOutput = await Mediator.Send(new TestCommandWithResult { Id = Command.Id }, cancellationToken);

        if (!commandOutput.Success)
            return new CommandWorkflowResult(commandOutput.ValidationResults);

        // Success is guaranteed here, so Result will not be null
        return new CommandWorkflowResult<TestCommandResult>(commandOutput.Result<TestCommandResult>()!);
    }
}
