using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;

namespace AsyncMediator.Tests;

[TestClass]
public class PipelineBehaviorTests
{
    [TestMethod]
    public async Task Send_WithNoBehaviors_ShouldExecuteHandler()
    {
        // Arrange
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);
        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task Send_WithEmptyBehaviors_ShouldExecuteHandler()
    {
        // Arrange
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory, behaviors: []);
        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task Send_WithSingleBehavior_ShouldExecuteBehaviorAndHandler()
    {
        // Arrange
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "First")]);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new[] { "First-Before", "First-After" }, executionOrder);
    }

    [TestMethod]
    public async Task Send_WithMultipleBehaviors_ShouldExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors:
            [
                new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "First"),
                new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "Second"),
                new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "Third")
            ]);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(
            new[] { "First-Before", "Second-Before", "Third-Before", "Third-After", "Second-After", "First-After" },
            executionOrder);
    }

    [TestMethod]
    public async Task Send_WithShortCircuitBehavior_ShouldNotCallHandler()
    {
        // Arrange
        var handlerCalled = false;
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [new ShortCircuitBehavior<TestCommand>("Short-circuited")]);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new CallbackCommandHandler(mediator, () => handlerCalled = true));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual("Short-circuited", result.ValidationResults.First().ErrorMessage);
        Assert.IsFalse(handlerCalled);
    }

    [TestMethod]
    public async Task Send_BehaviorReceivesCancellationToken()
    {
        // Arrange
        CancellationToken receivedToken = default;
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [new TokenCaptureBehavior<TestCommand, ICommandWorkflowResult>(ct => receivedToken = ct)]);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        using var cts = new CancellationTokenSource();
        var command = new TestCommand { Id = 1 };

        // Act
        await mediator.Send(command, cts.Token);

        // Assert
        Assert.AreEqual(cts.Token, receivedToken);
    }

    [TestMethod]
    public async Task Send_BehaviorCanCatchExceptions()
    {
        // Arrange
        Exception? caughtException = null;
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [new ExceptionCatchingBehavior<TestCommand, ICommandWorkflowResult>(ex => caughtException = ex)]);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(
            new ThrowingCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsNotNull(caughtException);
        Assert.AreEqual("Handler threw an exception", caughtException.Message);
    }

    [TestMethod]
    public async Task Query_WithBehavior_ShouldExecuteBehavior()
    {
        // Arrange
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();
        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [new OrderTrackingBehavior<TestQueryCriteria, TestQueryResult>(executionOrder, "QueryBehavior")]);

        registry.AddHandlersForCommandOrQuery<IQuery<TestQueryCriteria, TestQueryResult>>(new TestQuery());

        var criteria = new TestQueryCriteria { SearchTerm = "test" };

        // Act
        var result = await mediator.Query<TestQueryCriteria, TestQueryResult>(criteria);

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "QueryBehavior-Before", "QueryBehavior-After" }, executionOrder);
    }

    [TestMethod]
    public async Task Send_WithBehaviorFactory_ShouldResolveBehaviorsFromFactory()
    {
        // Arrange
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();
        var behavior = new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "Factory");

        // Create a simple factory that returns behaviors based on type
        BehaviorFactory factory = type =>
        {
            if (type == typeof(IPipelineBehavior<TestCommand, ICommandWorkflowResult>))
                return [behavior];
            return [];
        };

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviorFactory: factory);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
        CollectionAssert.AreEqual(new[] { "Factory-Before", "Factory-After" }, executionOrder);
    }

    [TestMethod]
    public async Task Send_WithBothExplicitAndFactoryBehaviors_ShouldExecuteBothInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();
        var explicitBehavior = new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "Explicit");
        var factoryBehavior = new OrderTrackingBehavior<TestCommand, ICommandWorkflowResult>(executionOrder, "Factory");

        BehaviorFactory factory = type =>
        {
            if (type == typeof(IPipelineBehavior<TestCommand, ICommandWorkflowResult>))
                return [factoryBehavior];
            return [];
        };

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [explicitBehavior],
            behaviorFactory: factory);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
        // Explicit behaviors run first, then factory behaviors
        CollectionAssert.AreEqual(
            new[] { "Explicit-Before", "Factory-Before", "Factory-After", "Explicit-After" },
            executionOrder);
    }

    [TestMethod]
    public async Task Send_WithBehaviorFactoryReturningEmpty_ShouldExecuteHandlerDirectly()
    {
        // Arrange
        var registry = new TestHandlerRegistry();
        BehaviorFactory factory = _ => []; // Always returns empty

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviorFactory: factory);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act
        var result = await mediator.Send(command);

        // Assert
        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task Send_FactoryBehaviorsCached_ShouldOnlyCallFactoryOnce()
    {
        // Arrange
        var factoryCallCount = 0;
        var registry = new TestHandlerRegistry();

        BehaviorFactory factory = type =>
        {
            factoryCallCount++;
            return [];
        };

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviorFactory: factory);

        registry.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        registry.AddHandlersForEvent<IEventHandler<FakeEvent>>([new FakeEventHandler()]);
        registry.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new FakeEventFromHandlerHandler()]);

        var command = new TestCommand { Id = 1 };

        // Act - Send multiple commands of the same type
        await mediator.Send(command);
        await mediator.Send(command);
        await mediator.Send(command);

        // Assert - Factory should only be called once (cached)
        Assert.AreEqual(1, factoryCallCount);
    }
}

#region Test Behaviors and Handlers

/// <summary>
/// Behavior that tracks execution order for verification.
/// </summary>
file sealed class OrderTrackingBehavior<TRequest, TResponse>(List<string> executionOrder, string name)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        executionOrder.Add($"{name}-Before");
        var response = await next();
        executionOrder.Add($"{name}-After");
        return response;
    }
}

/// <summary>
/// Behavior that short-circuits without calling next.
/// </summary>
file sealed class ShortCircuitBehavior<TRequest>(string errorMessage)
    : IPipelineBehavior<TRequest, ICommandWorkflowResult>
{
    public Task<ICommandWorkflowResult> Handle(
        TRequest request,
        RequestHandlerDelegate<ICommandWorkflowResult> next,
        CancellationToken cancellationToken)
    {
        // Don't call next - return error directly
        var result = new CommandWorkflowResult([
            new System.ComponentModel.DataAnnotations.ValidationResult(errorMessage)
        ]);
        return Task.FromResult<ICommandWorkflowResult>(result);
    }
}

/// <summary>
/// Behavior that captures the cancellation token for verification.
/// </summary>
file sealed class TokenCaptureBehavior<TRequest, TResponse>(Action<CancellationToken> capture)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        capture(cancellationToken);
        return await next();
    }
}

/// <summary>
/// Behavior that catches exceptions and returns a failed result.
/// </summary>
file sealed class ExceptionCatchingBehavior<TRequest, TResponse>(Action<Exception> onException)
    : IPipelineBehavior<TRequest, ICommandWorkflowResult>
{
    public async Task<ICommandWorkflowResult> Handle(
        TRequest request,
        RequestHandlerDelegate<ICommandWorkflowResult> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            onException(ex);
            return new CommandWorkflowResult([
                new System.ComponentModel.DataAnnotations.ValidationResult(ex.Message)
            ]);
        }
    }
}

/// <summary>
/// Command handler that invokes a callback for verification.
/// </summary>
file sealed class CallbackCommandHandler(IMediator mediator, Action callback) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        callback();
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

/// <summary>
/// Command handler that throws an exception for testing exception handling.
/// </summary>
file sealed class ThrowingCommandHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Handler threw an exception");
}

/// <summary>
/// Simple fake event handler for deferred events.
/// </summary>
file sealed class FakeEventHandler : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Simple fake event handler for deferred events from handler.
/// </summary>
file sealed class FakeEventFromHandlerHandler : IEventHandler<FakeEventFromHandler>
{
    public Task Handle(FakeEventFromHandler @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Test query criteria.
/// </summary>
file sealed class TestQueryCriteria
{
    public string SearchTerm { get; init; } = string.Empty;
}

/// <summary>
/// Test query result.
/// </summary>
file sealed class TestQueryResult
{
    public IReadOnlyList<string> Items { get; init; } = [];
}

/// <summary>
/// Test query handler.
/// </summary>
file sealed class TestQuery : IQuery<TestQueryCriteria, TestQueryResult>
{
    public Task<TestQueryResult> Query(TestQueryCriteria criteria, CancellationToken cancellationToken = default)
        => Task.FromResult(new TestQueryResult { Items = ["item1", "item2"] });
}

#endregion
