using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;

namespace AsyncMediator.Tests;

[TestClass]
public class ReliabilityTests
{
    [TestMethod]
    public async Task EventHandler_ThrowsException_ShouldPropagateAndNotCorruptState()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var throwingHandler = new ThrowingEventHandler();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([throwingHandler]);

        mediator.DeferEvent(new FakeEvent { Id = 1 });

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => mediator.ExecuteDeferredEvents());

        // Verify state is still usable after exception
        mediator.DeferEvent(new FakeEvent { Id = 2 });
        // Should not throw on this subsequent operation
    }

    [TestMethod]
    public async Task CommandHandler_ValidationFailure_ShouldReturnErrorsNotThrow()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        // Act - Id 999 triggers validation failure in TestCommandHandler
        var result = await mediator.Send(new TestCommand { Id = 999 });

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Factory_ReturnsNull_ShouldThrowMeaningfulException()
    {
        // Arrange
        var handlerFactory = new NullReturningHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<NullReferenceException>(
            () => mediator.Send(new TestCommand { Id = 1 }));
    }

    [TestMethod]
    public async Task MultipleValidationErrors_ShouldAccumulateAllErrors()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<MultipleValidationErrorsCommand>>(
            new MultipleValidationErrorsHandler(mediator));

        // Act
        var result = await mediator.Send(new MultipleValidationErrorsCommand());

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(3, result.ValidationResults.Count, "Should have 3 validation errors");
    }

    [TestMethod]
    public async Task MemoryStability_HighVolumeEvents_ShouldNotLeakMemory()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Process many events in batches
        for (var batch = 0; batch < 100; batch++)
        {
            for (var i = 0; i < 1000; i++)
            {
                mediator.DeferEvent(new FakeEvent { Id = i });
            }
            await mediator.ExecuteDeferredEvents();

            // Force GC to clean up
            if (batch % 10 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);

        // Assert - Memory should not grow significantly (allow 10MB growth for test overhead)
        var memoryGrowth = finalMemory - initialMemory;
        Assert.IsTrue(memoryGrowth < 10 * 1024 * 1024,
            $"Memory grew by {memoryGrowth / 1024 / 1024}MB, possible memory leak");
    }
}

public class ThrowingEventHandler : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Intentional test exception");
}

public class NoOpEventHandler : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NoOpEventFromHandlerHandler : IEventHandler<FakeEventFromHandler>
{
    public Task Handle(FakeEventFromHandler @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NullReturningHandlerRegistry
{
    public IEnumerable<object> MultiInstanceFactory(Type type) => [];
    public object SingleInstanceFactory(Type type) => null!;
}

public sealed class MultipleValidationErrorsCommand : ICommand { }

public sealed class MultipleValidationErrorsHandler(IMediator mediator)
    : CommandHandler<MultipleValidationErrorsCommand>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken)
    {
        validationContext.AddError("Field1", "Error 1");
        validationContext.AddError("Field2", "Error 2");
        validationContext.AddError("Field3", "Error 3");
        return Task.CompletedTask;
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken) =>
        Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
}
