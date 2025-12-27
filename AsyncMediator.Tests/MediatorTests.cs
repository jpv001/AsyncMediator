using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;
using NSubstitute;

namespace AsyncMediator.Tests;

[TestClass]
public class MediatorTests
{
    private List<IEventHandler<FakeEvent>> _eventHandlers = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _eventHandlers = [];
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WhenCalledWithoutEvent_ShouldNotThrow()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        // Act
        await mediator.ExecuteDeferredEvents();
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WhenCalled_ShouldCallAllEventHandlers()
    {
        // Arrange
        var @event = new FakeEvent { Id = 1 };
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var handler1 = Substitute.For<HandlerDeferringMultipleEvents>(mediator);
        var handler2 = Substitute.For<HandlerDeferringSingleEvent>(mediator);
        var handler3 = Substitute.For<HandlerWithoutAdditionalEvents>();

        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([handler1, handler2, handler3]);

        mediator.DeferEvent(@event);

        // Act
        await mediator.ExecuteDeferredEvents();

        // Assert
        foreach (var handler in handlerFactory.GetHandlersFor<FakeEvent>())
        {
            handler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();
        }
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WhenCalledWithoutRegisteredHandlers_ShouldNotCallAnyHandlers()
    {
        // Arrange
        var @event = new FakeEvent { Id = 1 };
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        mediator.DeferEvent(@event);

        // Act
        await mediator.ExecuteDeferredEvents();

        // Assert - No handlers to check since none were registered
        foreach (var handler in handlerFactory.GetHandlersFor<FakeEvent>())
        {
            handler.DidNotReceive().Handle(Arg.Any<FakeEvent>()).FireAndForget();
        }
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WhenCalledWithInterface_ShouldCallAllEventHandlers()
    {
        // Arrange
        var @event = new FakeEvent { Id = 1 };
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var handler1 = Substitute.For<HandlerForInterfaceDeferringMultipleEvents>(mediator);
        var handler2 = Substitute.For<HandlerForInterfaceDeferringSingleEvent>(mediator);
        var handler3 = Substitute.For<HandlerForInterfaceWithoutAdditionalEvents>();

        handlerFactory.AddHandlersForEvent<IEventHandler<IFakeEvent>>([handler1, handler2, handler3]);

        mediator.DeferEvent<IFakeEvent>(@event);

        // Act
        await mediator.ExecuteDeferredEvents();

        // Assert
        foreach (var handler in handlerFactory.GetHandlersFor<IFakeEvent>())
        {
            handler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();
        }
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_WhenCalled_ShouldExecuteEventHandlersForEventsFiredInHandlers()
    {
        // Arrange
        var triggerEvent = new FakeEvent { Id = 1 };
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>(
        [
            new HandlerDeferringMultipleEvents(mediator),
            new HandlerDeferringSingleEvent(mediator),
            new HandlerWithoutAdditionalEvents()
        ]);

        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>(
        [
            new DependentEventHandler()
        ]);

        var chainedHandler = Substitute.For<ChainedEventHandler>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventTwoFromHandler>>([chainedHandler]);

        // Act
        mediator.DeferEvent(triggerEvent);
        await mediator.ExecuteDeferredEvents();

        // Assert
        chainedHandler.Received(1).Handle(Arg.Any<FakeEventTwoFromHandler>()).FireAndForget();
    }

    [TestMethod]
    public async Task ExecuteDeferredEvents_CanAddFromMultipleThreads()
    {
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var mockHandler = Substitute.For<HandlerWithoutAdditionalEvents>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([mockHandler]);

        var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };

        // Make sure the mediator can handle events being added from multiple threads
        Parallel.For(0, 10, options, i =>
        {
            for (var j = 0; j < 1000; ++j)
                mediator.DeferEvent(new FakeEvent { Id = (i * 1000) + j });
        });

        await mediator.ExecuteDeferredEvents();

        mockHandler.Received(10000).Handle(Arg.Any<FakeEvent>()).FireAndForget();
    }

    [TestMethod]
    [ExpectedException(typeof(MissingMethodException))]
    public async Task CommandMissing_ShouldThrowEx()
    {
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var mockHandler = Substitute.For<HandlerWithoutAdditionalEvents>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([mockHandler]);

        await mediator.Send(new CommandMissing { Id = 1 });
    }

    // Note: EventHandlerOrdering_ShouldOrderHandlersByAttribute was removed in v3.0.0
    // Handler ordering is now controlled by DI registration order instead of attributes.

    [TestMethod]
    public async Task DeferEvents_CanDeferMultipleEvents()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var fakeEventHandler1 = Substitute.For<HandlerDeferringMultipleEvents>(mediator);
        var fakeEventHandler2 = Substitute.For<HandlerDeferringSingleEvent>(mediator);
        var fakeEventHandler3 = Substitute.For<HandlerWithoutAdditionalEvents>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([fakeEventHandler1, fakeEventHandler2, fakeEventHandler3]);

        var dependentHandler = Substitute.For<DependentEventHandler>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([dependentHandler]);

        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommand { Id = 1 });
        Assert.IsFalse(result.ValidationResults.Any());

        // Assert
        foreach (var handler in handlerFactory.GetHandlersFor<FakeEvent>())
        {
            handler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();
        }

        foreach (var handler in handlerFactory.GetHandlersFor<FakeEventFromHandler>())
        {
            handler.Received().Handle(Arg.Any<FakeEventFromHandler>()).FireAndForget();
        }
    }

    [TestMethod]
    public async Task Commands_CanHandleCommand()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommand { Id = 1 });

        // Assert
        Assert.IsFalse(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Commands_CanHandleCommandWithAReturnValue()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommandWithResult { Id = 1 });

        // Assert
        Assert.AreEqual(5, result.Result<TestCommandResult>()!.ResultingValue);
    }

    [TestMethod]
    public async Task Commands_CanHandleCommandThatFiresOtherCommandsWithAReturnValue()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestMultipleCommandWithResult>>(new TestMultipleCommandHandlerWithResult(mediator));

        // Act
        var result = await mediator.Send(new TestMultipleCommandWithResult { Name = "bar" });

        // Assert
        Assert.AreEqual(5, result.Result<TestCommandResult>()!.ResultingValue);
    }

    [TestMethod]
    public async Task Commands_CanHandleCommandThatFiresOtherCommandsWithATopLevelError()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestMultipleCommandWithResult>>(new TestMultipleCommandHandlerWithResult(mediator));

        // Act
        var result = await mediator.Send(new TestMultipleCommandWithResult { Name = "foo" });

        // Assert
        Assert.IsNull(result.Result<TestCommandResult>());
        Assert.IsTrue(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Commands_CanHandleCommandThatFiresOtherCommandsWithANestedError()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestMultipleCommandWithResult>>(new TestMultipleCommandHandlerWithResult(mediator));

        // Act
        var result = await mediator.Send(new TestMultipleCommandWithResult { Name = "bar", Id = 999 });

        // Assert
        Assert.IsNull(result.Result<TestCommandResult>());
        Assert.IsTrue(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Commands_CanHandleCommandWithAReturnValueWithValidationFailures()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommandWithResult { Id = 999 });
        var returnedValue = result.Result<TestCommandResult>();

        // Assert
        Assert.IsNull(returnedValue);
        Assert.IsTrue(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Commands_WhenExecuting_CanHandleValidationErrors()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommand { Id = 999 });

        // Assert
        Assert.IsTrue(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Commands_WhenExecuting_CanSuccessfulCompleteValidCommand()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        // Act
        var result = await mediator.Send(new TestCommand { Id = 1 });

        // Assert
        Assert.IsFalse(result.ValidationResults.Any());
    }

    [TestMethod]
    public async Task Queries_WhenCalledWithCriteria_ShouldReturnResult()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());

        // Act
        var result = await mediator.Query<FakeRangeCriteria, List<FakeResult>>(new FakeRangeCriteria { MinValue = 1, MaxValue = 5 });

        // Assert
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public async Task Queries_WhenCalledWithPrimitiveCriteria_ShouldReturnResult()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<int, List<FakeResult>>>(new FindFakeResultByPrimitiveType());

        // Act
        var result = await mediator.Query<int, List<FakeResult>>(1);

        // Assert
        Assert.IsNotNull(result.FirstOrDefault());
        Assert.AreEqual(1, result.First().Id);
    }

    [TestMethod]
    public async Task Queries_CanReturnPrimitiveTypes()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, int>>(new FindPrimitiveTypeByCriteria());

        // Act
        var result = await mediator.Query<SingleNameCriteria, int>(new SingleNameCriteria { Name = "Name1" });

        // Assert
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public async Task Queries_ShouldAllowMultipleQueryDefinitionsPerObject()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleIdCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());

        // Act
        var resultByName = await mediator.Query<SingleNameCriteria, FakeResult>(new SingleNameCriteria { Name = "Name2" });
        var resultById = await mediator.Query<SingleIdCriteria, FakeResult>(new SingleIdCriteria { Id = 1 });

        // Assert
        Assert.AreEqual(2, resultByName.Id);
        Assert.AreEqual(1, resultById.Id);
    }

    [TestMethod]
    public async Task LookupQuery_CanLookupData()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ILookupQuery<List<FakeResult>>>(new FindResultForLookup());

        // Act
        var result = await mediator.LoadList<List<FakeResult>>();

        // Assert
        Assert.AreEqual(FakeDataStore.Results.Count, result.Count);
    }
}
