using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace AsyncMediator.Tests;

[TestClass]
public class SafeByDefaultBehaviorTests
{
    [TestMethod]
    public async Task EventsOnlyExecuteOnSuccess_ValidationFailure_EventsNotExecuted()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        var eventExecuted = false;

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new ValidationFailureCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new TrackingEventHandler(() => eventExecuted = true)
        ]);

        var result = await mediator.Send(new TestCommand { Id = 999 });

        Assert.IsFalse(result.Success, "Command should fail validation");
        Assert.AreEqual(1, result.ValidationResults.Count);
        Assert.IsFalse(eventExecuted, "Events should NOT execute when validation fails");
    }

    [TestMethod]
    public async Task EventsOnlyExecuteOnSuccess_DoHandleReturnsFailure_EventsNotExecuted()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        var eventExecuted = false;

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new ReturnsFailureCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new TrackingEventHandler(() => eventExecuted = true)
        ]);

        var result = await mediator.Send(new TestCommand { Id = 1 });

        Assert.IsFalse(result.Success, "Command should return failure result");
        Assert.AreEqual(1, result.ValidationResults.Count);
        Assert.IsFalse(eventExecuted, "Events should NOT execute when DoHandle returns failure");
    }

    [TestMethod]
    public async Task EventsOnlyExecuteOnSuccess_Success_EventsExecuted()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        var eventExecuted = false;

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new SuccessCommandHandlerWithDeferredEvent(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new TrackingEventHandler(() => eventExecuted = true)
        ]);

        var result = await mediator.Send(new TestCommand { Id = 1 });

        Assert.IsTrue(result.Success, "Command should succeed");
        Assert.IsTrue(eventExecuted, "Events SHOULD execute when command succeeds");
    }

    [TestMethod]
    public async Task EventQueueClearedOnException_HandlerThrows_QueueIsCleared()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new ThrowingCommandHandlerWithDeferredEvent(mediator));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => mediator.Send(new TestCommand { Id = 1 }));

        mediator.DeferEvent(new FakeEvent { Id = 99 });
        var queueCount = 0;
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new TrackingEventHandler(() => queueCount++)
        ]);

        await mediator.ExecuteDeferredEvents();

        Assert.AreEqual(1, queueCount, "Only the newly deferred event should execute, previous events should be cleared");
    }

    [TestMethod]
    public async Task EventQueueClearedOnException_SubsequentCommandSucceeds_NoLeakedEvents()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        var eventsExecuted = new List<int>();

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new ConditionalThrowingCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new TrackingEventHandlerWithId(id => eventsExecuted.Add(id))
        ]);

        try
        {
            await mediator.Send(new TestCommand { Id = 100 });
        }
        catch (InvalidOperationException)
        {
        }

        var result = await mediator.Send(new TestCommand { Id = 1 });

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, eventsExecuted.Count, "Only events from successful command should execute");
        Assert.AreEqual(1, eventsExecuted[0], "Should be event from second command, not first");
    }

    [TestMethod]
    public async Task TransactionScope_EventsExecuteAfterCommit_NotDuring()
    {
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        var executionOrder = new List<string>();

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new TransactionalCommandHandlerWithTracking(mediator, executionOrder));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([
            new OrderTrackingEventHandler(executionOrder)
        ]);

        var result = await mediator.Send(new TestCommand { Id = 1 });

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, executionOrder.Count);
        Assert.AreEqual("DoHandle", executionOrder[0], "Handler logic executes first");
        Assert.AreEqual("EventHandler", executionOrder[1], "Event handlers execute AFTER DoHandle completes");
    }
}

file sealed class ValidationFailureCommandHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken)
    {
        if (Command.Id == 999)
            context.AddError(nameof(Command.Id), "Invalid ID");
        return Task.CompletedTask;
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent { Id = Command.Id });
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

file sealed class ReturnsFailureCommandHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent { Id = Command.Id });
        return Task.FromResult<ICommandWorkflowResult>(new CommandWorkflowResult("Business rule failure"));
    }
}

file sealed class SuccessCommandHandlerWithDeferredEvent(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent { Id = Command.Id });
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

file sealed class ThrowingCommandHandlerWithDeferredEvent(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent { Id = 999 });
        throw new InvalidOperationException("Handler failure");
    }
}

file sealed class ConditionalThrowingCommandHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent { Id = Command.Id });

        if (Command.Id == 100)
            throw new InvalidOperationException("Intentional failure");

        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

file sealed class TransactionalCommandHandlerWithTracking(IMediator mediator, List<string> executionOrder)
    : CommandHandler<TestCommand>(mediator)
{
    protected override bool UseTransactionScope => true;

    protected override Task Validate(ValidationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context, CancellationToken cancellationToken)
    {
        executionOrder.Add("DoHandle");
        Mediator.DeferEvent(new FakeEvent { Id = Command.Id });
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

file sealed class TrackingEventHandler(Action onExecute) : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        onExecute();
        return Task.CompletedTask;
    }
}

file sealed class TrackingEventHandlerWithId(Action<int> onExecute) : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        onExecute(@event.Id);
        return Task.CompletedTask;
    }
}

file sealed class OrderTrackingEventHandler(List<string> executionOrder) : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        executionOrder.Add("EventHandler");
        return Task.CompletedTask;
    }
}
