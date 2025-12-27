using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;

namespace AsyncMediator.Tests;

[TestClass]
public class CancellationTests
{
    [TestMethod]
    public async Task Send_WithAlreadyCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => mediator.Send(new TestCommand { Id = 1 }, cts.Token));
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WithAlreadyCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);

        mediator.DeferEvent(new FakeEvent { Id = 1 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => mediator.ExecuteDeferredEvents(cts.Token));
    }

    [TestMethod]
    public async Task Query_WithAlreadyCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(
            new CancellationAwareQuery());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => mediator.Query<FakeRangeCriteria, List<FakeResult>>(
                new FakeRangeCriteria { MinValue = 1, MaxValue = 5 }, cts.Token));
    }

    [TestMethod]
    public async Task LoadList_WithAlreadyCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ILookupQuery<List<FakeResult>>>(
            new CancellationAwareLookupQuery());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => mediator.LoadList<List<FakeResult>>(cts.Token));
    }

    [TestMethod]
    public async Task Send_WithValidToken_ShouldSucceed()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        using var cts = new CancellationTokenSource();

        // Act
        var result = await mediator.Send(new TestCommand { Id = 1 }, cts.Token);

        // Assert
        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WithMultipleEvents_CancellationStopsProcessing()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var counter = new EventCounter();
        var handler = new CancellationTestEventHandler(counter);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([handler]);

        // Defer multiple events
        for (var i = 0; i < 10; i++)
        {
            mediator.DeferEvent(new FakeEvent { Id = i });
        }

        using var cts = new CancellationTokenSource();
        // Cancel after 3 events
        handler.CancelAfter(3, cts);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => mediator.ExecuteDeferredEvents(cts.Token));

        // Should have processed exactly 3 events before cancellation
        Assert.AreEqual(3, counter.Count);
    }

    [TestMethod]
    public async Task CommandHandler_CancellationTokenProperty_IsAccessible()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        var handler = new CancellationTokenCapturingHandler(mediator);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(handler);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        using var cts = new CancellationTokenSource();

        // Act
        await mediator.Send(new TestCommand { Id = 1 }, cts.Token);

        // Assert
        Assert.IsTrue(handler.TokenWasCaptured);
        Assert.IsFalse(handler.CapturedToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task Send_CancellationDuringValidation_ShouldThrow()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        using var cts = new CancellationTokenSource();
        var handler = new SlowValidationHandler(mediator, cts, cancelDuringValidation: true);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(handler);

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        var exception = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
            () => mediator.Send(new TestCommand { Id = 1 }, cts.Token));
        Assert.IsInstanceOfType(exception, typeof(OperationCanceledException));
    }

    [TestMethod]
    public async Task Send_CancellationDuringDoHandle_ShouldThrow()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        using var cts = new CancellationTokenSource();
        var handler = new SlowValidationHandler(mediator, cts, cancelDuringValidation: false);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(handler);

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        var exception = await Assert.ThrowsExceptionAsync<TaskCanceledException>(
            () => mediator.Send(new TestCommand { Id = 1 }, cts.Token));
        Assert.IsInstanceOfType(exception, typeof(OperationCanceledException));
    }
}

// Test helpers

public class CancellationAwareQuery : IQuery<FakeRangeCriteria, List<FakeResult>>
{
    public Task<List<FakeResult>> Query(FakeRangeCriteria criteria, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FakeDataStore.Results
            .Where(x => x.Id <= criteria.MaxValue && x.Id >= criteria.MinValue)
            .ToList());
    }
}

public class CancellationAwareLookupQuery : ILookupQuery<List<FakeResult>>
{
    public Task<List<FakeResult>> Query(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FakeDataStore.Results.ToList());
    }
}

public class CancellationTestEventHandler(EventCounter counter) : IEventHandler<FakeEvent>
{
    private CancellationTokenSource? _cts;
    private int _cancelAfterCount;

    public void CancelAfter(int count, CancellationTokenSource cts)
    {
        _cancelAfterCount = count;
        _cts = cts;
    }

    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        counter.Increment();
        if (_cts != null && counter.Count >= _cancelAfterCount)
        {
            _cts.Cancel();
        }
        return Task.CompletedTask;
    }
}

public class CancellationTokenCapturingHandler(IMediator mediator) : CommandHandler<TestCommand>(mediator)
{
    public bool TokenWasCaptured { get; private set; }
    public CancellationToken CapturedToken { get; private set; }

    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        TokenWasCaptured = true;
        CapturedToken = CancellationToken; // Access the protected property
        return Task.CompletedTask;
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        Mediator.DeferEvent(new FakeEvent());
        Mediator.DeferEvent(new FakeEventFromHandler());
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}

public class SlowValidationHandler(IMediator mediator, CancellationTokenSource cts, bool cancelDuringValidation)
    : CommandHandler<TestCommand>(mediator)
{
    protected override async Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (cancelDuringValidation)
        {
            cts.Cancel();
            await Task.Delay(10, cancellationToken); // This will throw due to cancellation
        }
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (!cancelDuringValidation)
        {
            cts.Cancel();
            await Task.Delay(10, cancellationToken); // This will throw due to cancellation
        }
        return CommandWorkflowResult.Ok();
    }
}

// Re-use NoOpEventHandler from ReliabilityTests - they're in the same namespace
