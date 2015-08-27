using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutofacContrib.NSubstitute;
using FizzWare.NBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.ComponentModel.DataAnnotations;
using AsyncMediator;

namespace AsyncMediator.Test
{
    [TestClass]
    public class MediatorTests
    {
        private static readonly List<FakeResult> FakeDataStore = Builder<FakeResult>.CreateListOfSize(10).Build().ToList();
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
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            await mediator.ExecuteDeferredEvents();
        }


        [TestMethod]
        public async Task ExecuteDeferredEvents_WhenCalled_ShouldCallAllEventHandlers()
        {
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
            await mediator.ExecuteDeferredEvents();

            foreach (var handler in handlerFactory.GetHandlersFor<FakeEvent>())
                handler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();
        }

        [TestMethod]
        public async Task ExecuteDeferredEvents_WhenCalled_ShouldExecuteEventHandlersForEventsFiredInHandlers()
        {
            //Arrange
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

            //Act
            mediator.DeferEvent(triggerEvent);
            await mediator.ExecuteDeferredEvents();

            //Assert
            foreach (var handler in handlerFactory.GetHandlersFor<FakeEventTwoFromHandler>())
                handler.Received(1).Handle(Arg.Any<FakeEventTwoFromHandler>()).FireAndForget();
        }

        [TestMethod]
        public void EventHandlerOrdering_ShouldOrderHandlersByAttribute()
        {
            //These are added in reverse order to make sure the handler sort works
            _eventHandlers.Add(Substitute.For<HandlerWithoutEventsWithOrdering>());
            _eventHandlers.Add(Substitute.For<HandlerWithoutAdditionalEvents>());
            _eventHandlers.Add(Substitute.For<HandlerWithoutEventsWithoutOrdering>());

            var executionOrder = _eventHandlers.OrderByExecutionOrder().ToList();

            Assert.IsTrue(executionOrder[0] == _eventHandlers[2]);
            Assert.IsTrue(executionOrder[1] == _eventHandlers[1]);
            Assert.IsTrue(executionOrder[2] == _eventHandlers[0]);
        }


        [TestMethod]
        public async Task DeferEvents_CanDeferMultipleEvents()
        {
            //Arrange
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

            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestTetraCommandHandler(mediator));

            //Act
            var result = await mediator.Send(new TestCommand { Id = 1 });
            Assert.IsFalse(result.ValidationResults.Any());

            //Assert
            foreach (var handler in handlerFactory.GetHandlersFor<FakeEvent>())
                handler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();

            foreach (var handler in handlerFactory.GetHandlersFor<FakeEventFromHandler>())
                handler.Received().Handle(Arg.Any<FakeEventFromHandler>()).FireAndForget();
        }

        [TestMethod]
        public async Task Commands_CanHandleCommand()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestTetraCommandHandler(mediator));

            var result = await mediator.Send(new TestCommand { Id = 1 });
            Assert.IsFalse(result.ValidationResults.Any());
        }

        [TestMethod]
        public async Task Commands_WhenExecuting_CanHandleValidationErrors()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestTetraCommandHandler(mediator));

            var result = await mediator.Send(new TestCommand { Id = 999 });
            Assert.IsTrue(result.ValidationResults.Any());
        }

        [TestMethod]
        public async Task Commands_WhenExecuting_CanSuccessfulCompleteValidCommand()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestTetraCommandHandler(mediator));

            var result = await mediator.Send(new TestCommand { Id = 1 });
            Assert.IsFalse(result.ValidationResults.Any());
        }

        [TestMethod]
        public async Task Queries_WhenCalledWithCriteria_ShouldReturnResult()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());

            var result = await mediator.Query<FakeRangeCriteria, List<FakeResult>>(new FakeRangeCriteria { MinValue = 0, MaxValue = 5 });
            Assert.IsTrue(result.Count == 5);
        }


        [TestMethod]
        public async Task Queries_WhenCalledWithPrimitiveCriteria_ShouldReturnResult()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<int, List<FakeResult>>>(new FindFakeResultByPrimitiveType());

            var result = await mediator.Query<int, List<FakeResult>>(1);
            Assert.IsTrue(result.FirstOrDefault() != null);
            Assert.IsTrue(result.First().Id == 1);
        }

        [TestMethod]
        public async Task Queries_CanReturnPrimitiveTypes()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, int>>(new FindPrimitiveTypeByCriteria());

            var result = await mediator.Query<SingleNameCriteria, int>(new SingleNameCriteria { Name = "Name1" });
            Assert.IsTrue(result == 1);
        }

        [TestMethod]
        public async Task Queries_ShouldAllowMultipleQueryDefinitionsPerObject()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleIdCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());
            handlerFactory.AddHandlersForCommandOrQuery<IQuery<SingleNameCriteria, FakeResult>>(new MultipleQueryTypesInOneObject());

            var resultByName = await mediator.Query<SingleNameCriteria, FakeResult>(new SingleNameCriteria { Name = "Name2" });
            var resultById = await mediator.Query<SingleIdCriteria, FakeResult>(new SingleIdCriteria { Id = 1 });

            Assert.IsTrue(resultByName.Id == 2);
            Assert.IsTrue(resultById.Id == 1);
        }

        [TestMethod]
        public async Task LookupQuery_CanLookupData()
        {
            var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
            handlerFactory.AddHandlersForCommandOrQuery<ILookupQuery<List<FakeResult>>>(new FindResultForLookup());

            var result = await mediator.LoadList<List<FakeResult>>();
            Assert.IsTrue(result.Count == FakeDataStore.Count);
        }

        #region Test Query classes

        class FindResultForLookup : ILookupQuery<List<FakeResult>>
        {
            public async Task<List<FakeResult>> Query()
            {
                return await Task.FromResult(FakeDataStore.ToList());
            }
        }

        class FindFakeResultByPrimitiveType : IQuery<int, List<FakeResult>>
        {
            public Task<List<FakeResult>> Query(int criteria)
            {
                var result = FakeDataStore.Where(x => x.Id == criteria).ToList();
                return Task.FromResult(result);
            }
        }

        class FindPrimitiveTypeByCriteria : IQuery<SingleNameCriteria, int>
        {

            public Task<int> Query(SingleNameCriteria criteria)
            {
                var result = FakeDataStore.Single(x => x.Name == criteria.Name);
                return Task.FromResult(result.Id);
            }
        }

        class FindFakeResultByRangeCriteria : IQuery<FakeRangeCriteria, List<FakeResult>>
        {
            public Task<List<FakeResult>> Query(FakeRangeCriteria criteria)
            {
                var result = FakeDataStore.Where(x => x.Id <= criteria.MaxValue && x.Id >= criteria.MinValue).ToList();
                return Task.FromResult(result);
            }
        }

        class MultipleQueryTypesInOneObject : IQuery<SingleIdCriteria, FakeResult>, IQuery<SingleNameCriteria, FakeResult>
        {
            public Task<FakeResult> Query(SingleIdCriteria criteria)
            {
                var result = FakeDataStore.SingleOrDefault(x => x.Id == criteria.Id);
                return Task.FromResult(result);
            }

            public Task<FakeResult> Query(SingleNameCriteria criteria)
            {
                var result = FakeDataStore.SingleOrDefault(x => x.Name == criteria.Name);
                return Task.FromResult(result);
            }
        }

        #endregion

        #region Test Stubs for Queries

        class FakeResult
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string RelatedData { get; set; }
        }

        class FakeRangeCriteria
        {
            public int MaxValue { get; set; }
            public int MinValue { get; set; }
        }

        class SingleNameCriteria
        {
            public string Name { get; set; }
        }

        class SingleIdCriteria
        {
            public int Id { get; set; }
        }

        #endregion

        #region Test Stubs for eventing
        // We need a concrete class here because IEventHandler<> expects a generic parameter. This is used
        // when setting up evernt handlers above.
        public class FakeEvent : IDomainEvent
        {
            public int Id { get; set; }

        }

        public class FakeEventFromHandler : IDomainEvent
        {
            public int Id { get; set; }
        }

        public class FakeEventTwoFromHandler : IDomainEvent
        {
            public int Id { get; set; }
        }

        [HandlerOrder(1)]
        public class HandlerDeferringSingleEvent : IEventHandler<FakeEvent>
        {
            private readonly IMediator _mediator;
            public HandlerDeferringSingleEvent(IMediator mediator)
            {
                _mediator = mediator;
            }

            public virtual Task Handle(FakeEvent @event)
            {
                _mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
                return Task.FromResult(1);
            }
        }

        [HandlerOrder(2)]
        public class HandlerDeferringMultipleEvents : IEventHandler<FakeEvent>
        {
            private readonly IMediator _mediator;
            public HandlerDeferringMultipleEvents(IMediator mediator)
            {
                _mediator = mediator;
            }

            public virtual Task Handle(FakeEvent @event)
            {
                _mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
                _mediator.DeferEvent(new FakeEventTwoFromHandler { Id = 1 });
                return Task.FromResult(2);
            }
        }

        [HandlerOrder(1)]
        public class HandlerWithoutAdditionalEvents : IEventHandler<FakeEvent>
        {
            public virtual Task Handle(FakeEvent @event)
            {
                return Task.FromResult(3);
            }
        }

        [HandlerOrder(2)]
        public class HandlerWithoutEventsWithOrdering : IEventHandler<FakeEvent>
        {
            public virtual Task Handle(FakeEvent @event)
            {
                return Task.FromResult(5);
            }
        }

        public class HandlerWithoutEventsWithoutOrdering : IEventHandler<FakeEvent>
        {
            public virtual Task Handle(FakeEvent @event)
            {
                return Task.FromResult(5);
            }
        }

        public class DependentEventHandler : IEventHandler<FakeEventFromHandler>
        {
            public virtual Task Handle(FakeEventFromHandler @event)
            {
                return Task.FromResult(4);
            }
        }

        public class ChainedEventHandler : IEventHandler<FakeEventTwoFromHandler>
        {
            public virtual Task Handle(FakeEventTwoFromHandler @event)
            {
                return Task.FromResult(6);
            }
        }

        public class TestCommand : ICommand
        {
            public int Id { get; set; }
        }

        public class TestTetraCommandHandler : CommandHandler<TestCommand>
        {
            private readonly IMediator _mediator;

            public TestTetraCommandHandler(IMediator mediator)
                : base(mediator)
            {
                _mediator = mediator;
            }

            protected override Task Validate(ValidationContext validationContext)
            {
                if (Command.Id == 999)
                    validationContext.AddError("UserId", "Validation Failed");
                return Task.FromResult(0);
            }

            protected override Task DoHandle(ValidationContext validationContext)
            {
                _mediator.DeferEvent(new FakeEvent());
                _mediator.DeferEvent(new FakeEventFromHandler());
                return Task.FromResult(0);
            }
        }

        #endregion
    }
}
