using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutofacContrib.NSubstitute;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace AsyncMediator.Test
{
    [TestClass]
    public class MediatorTests
    {
        private List<IEventHandler<FakeEvent>> _eventHandlers;
        private AutoSubstitute _autoSubstitute;

        [TestInitialize]
        public void TestInitialize()
        {
            _autoSubstitute = new AutoSubstitute();
            _eventHandlers = new List<IEventHandler<FakeEvent>>();
        }

        [TestMethod]
        public async Task ExecuteDeferredEvents_WhenCalledWithoutEvent_ShouldNotThrow()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

            // Act
            await mediator.ExecuteDeferredEvents();
        }

        [TestMethod]
        public async Task ExecuteDeferredEvents_WhenCalled_ShouldCallAllEventHandlers()
        {
            // Arrange
            var @event = new FakeEvent { Id = 1 };
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEvent>>
            {
                _autoSubstitute.SubstituteFor<HandlerDeferringMultipleEvents>(mediator),
                _autoSubstitute.SubstituteFor<HandlerDeferringSingleEvent>(mediator),
                _autoSubstitute.SubstituteFor<HandlerWithoutAdditionalEvents>()
            });

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
        public async Task ExecuteDeferredEvents_WhenCalled_ShouldExecuteEventHandlersForEventsFiredInHandlers()
        {
            // Arrange
            var triggerEvent = new FakeEvent { Id = 1 };

            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEvent>>
            {
                new HandlerDeferringMultipleEvents(mediator),
                new HandlerDeferringSingleEvent(mediator),
                new HandlerWithoutAdditionalEvents()
            });

            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEventFromHandler>>
            {
                new DependentEventHandler()
            });

            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEventTwoFromHandler>>
            {
                _autoSubstitute.SubstituteFor<ChainedEventHandler>()
            });

            // Act
            mediator.DeferEvent(triggerEvent);
            await mediator.ExecuteDeferredEvents();

            // Assert
            foreach (var handler in handlerFactory.GetHandlersFor<FakeEventTwoFromHandler>())
            {
                handler.Received(1).Handle(Arg.Any<FakeEventTwoFromHandler>()).FireAndForget();
            }
        }

        [TestMethod]
        public void EventHandlerOrdering_ShouldOrderHandlersByAttribute()
        {
            // Arrange
            // These are added in reverse order to make sure the handler sort works
            _eventHandlers.Add(Substitute.For<HandlerWithoutEventsWithOrdering>());
            _eventHandlers.Add(Substitute.For<HandlerWithoutAdditionalEvents>());
            _eventHandlers.Add(Substitute.For<HandlerWithoutEventsWithoutOrdering>());

            // Act
            var executionOrder = _eventHandlers.OrderByExecutionOrder().ToList();

            // Assert
            Assert.IsTrue(executionOrder[0] == _eventHandlers[2]);
            Assert.IsTrue(executionOrder[1] == _eventHandlers[1]);
            Assert.IsTrue(executionOrder[2] == _eventHandlers[0]);
        }

        [TestMethod]
        public async Task DeferEvents_CanDeferMultipleEvents()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEvent>>
            {
                Substitute.For<HandlerDeferringMultipleEvents>(mediator),
                Substitute.For<HandlerDeferringSingleEvent>(mediator),
                Substitute.For<HandlerWithoutAdditionalEvents>()
            });

            handlerFactory.AddHandlersForEvent(new List<IEventHandler<FakeEventFromHandler>>
            {
                Substitute.For<DependentEventHandler>()
            });

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
            var handlerFactory = new MessageHandlerRegistry();
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
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));

            // Act
            var result = await mediator.Send(new TestCommandWithResult { Id = 1 });

            // Assert
            Assert.IsTrue(result.Result<TestCommandResult>().ResultingValue == 5);
        }


        [TestMethod]
        public async Task Commands_CanHandleCommandWithAReturnValueWithValidationFailures()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));

            // Act
            var result = await mediator.Send(new TestCommandWithResult { Id = 999 });
            var returnedValue = result.Result<TestCommandResult>();

            // Assert
            Assert.IsTrue(returnedValue == null && result.ValidationResults.Any());
        }

        [TestMethod]
        public async Task Commands_WhenExecuting_CanHandleValidationErrors()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
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
            var handlerFactory = new MessageHandlerRegistry();
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
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());

            // Act
            var result = await mediator.Query<FakeRangeCriteria, List<FakeResult>>(new FakeRangeCriteria { MinValue = 0, MaxValue = 5 });

            // Assert
            Assert.IsTrue(result.Count == 5);
        }

        [TestMethod]
        public async Task Queries_WhenCalledWithPrimitiveCriteria_ShouldReturnResult()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<int, List<FakeResult>>>(new FindFakeResultByPrimitiveType());

            // Act
            var result = await mediator.Query<int, List<FakeResult>>(1);

            // Assert
            Assert.IsTrue(result.FirstOrDefault() != null);
            Assert.IsTrue(result.First().Id == 1);
        }

        [TestMethod]
        public async Task Queries_CanReturnPrimitiveTypes()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, int>>(new FindPrimitiveTypeByCriteria());

            // Act
            var result = await mediator.Query<SingleNameCriteria, int>(new SingleNameCriteria { Name = "Name1" });

            // Assert
            Assert.IsTrue(result == 1);
        }

        [TestMethod]
        public async Task Queries_ShouldAllowMultipleQueryDefinitionsPerObject()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleIdCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());

            // Act
            var resultByName = await mediator.Query<SingleNameCriteria, FakeResult>(new SingleNameCriteria { Name = "Name2" });
            var resultById = await mediator.Query<SingleIdCriteria, FakeResult>(new SingleIdCriteria { Id = 1 });

            // Assert
            Assert.IsTrue(resultByName.Id == 2);
            Assert.IsTrue(resultById.Id == 1);
        }

        [TestMethod]
        public async Task LookupQuery_CanLookupData()
        {
            // Arrange
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ILookupQuery<List<FakeResult>>>(new FindResultForLookup());

            // Act
            var result = await mediator.LoadList<List<FakeResult>>();

            // Assert
            Assert.IsTrue(result.Count == FakeDataStore.Results.Count);
        }
    }
}
