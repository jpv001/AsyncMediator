using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator 
{
	public static class Program 
	{
		public static void Main() 
		{
			var handlerFactory = new MessageHandlerRegistry();
            var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
			Console.WriteLine("Hello Mediator!");

            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

            // Act
            var result = mediator.Send(new TestCommand { Id = 1 }).Result;

            // Assert
            if(!result.ValidationResults.Any())
            	Console.WriteLine("Test 1 Passed");


			handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
            handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestMultipleCommandWithResult>>(new TestMultipleCommandHandlerWithResult(mediator));
           
            // Act
           var result1 = mediator.Send(new TestMultipleCommandWithResult { Name = "bar" }).Result;

            if(result1.Result<TestCommandResult>().ResultingValue == 5)
            	Console.WriteLine("Test 2 Passed");

            // Act
            var result3 = mediator.Send(new TestCommandWithResult { Id = 999 }).Result;
            var returnedValue = result3.Result<TestCommandResult>();

            if(returnedValue == null && result3.ValidationResults.Any())
            	Console.WriteLine("Test 3 Passed");

            foreach(var msg in result3.ValidationResults)
            	Console.WriteLine(msg.ErrorMessage + " on " + msg.MemberName);
		}
	}

	public class TestMultipleCommandWithResult : ICommand
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

	public class FakeEvent : IDomainEvent
    {
        public int Id { get; set; }
    }

	public class FakeEventFromHandler : IDomainEvent
    {
        public int Id { get; set; }
    }

    public class TestCommandResult
    {
        public int ResultingValue { get; set; }
    }

	public class TestCommandWithResult : ICommand
    {
        public int Id { get; set; }
    }

    public class TestCommand : ICommand
    {
        public int Id { get; set; }
    }

	public class MessageHandlerRegistry
    {
        private readonly ConcurrentDictionary<Type, List<object>> _eventHandlers = new ConcurrentDictionary<Type, List<object>>();
        public Func<SingleInstanceFactory> SingleInstanceDelegate;
        public Func<MultiInstanceFactory> MultiInstanceDelegate;

        public MessageHandlerRegistry()
        {
            MultiInstanceDelegate = () => MultiInstanceFactory;
            SingleInstanceDelegate = () => SingleInstanceFactory;
        }

        public void AddHandlersForEvent<T>(List<T> handlers)
        {
            _eventHandlers.TryAdd(typeof(T), handlers.Cast<object>().ToList());
        }

        public void AddHandlersForCommandOrQuery<T>(T handler)
        {
            _eventHandlers.TryAdd(typeof(T), new List<object> { handler });
        }

        public IEnumerable<IEventHandler<T>> GetHandlersFor<T>() where T : IDomainEvent
        {
            if (!_eventHandlers.ContainsKey(typeof(IEventHandler<T>))) return new List<IEventHandler<T>>();
            return _eventHandlers[typeof(IEventHandler<T>)].Cast<IEventHandler<T>>();
        }

        public IEnumerable<object> MultiInstanceFactory(Type type)
        {
            return _eventHandlers.ContainsKey(type) ? _eventHandlers[type] : new List<object>();
        }

        public object SingleInstanceFactory(Type type)
        {
            return _eventHandlers.ContainsKey(type) ? _eventHandlers[type].FirstOrDefault() : Activator.CreateInstance(type);
        }
    }

        public class TestCommandWithResultHandler : CommandHandler<TestCommandWithResult>
    {
        private readonly IMediator _mediator;

        public TestCommandWithResultHandler(IMediator mediator)
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

        protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext)
        {
            _mediator.DeferEvent(new FakeEvent());
            _mediator.DeferEvent(new FakeEventFromHandler());
            var returnObject = new CommandWorkflowResult<TestCommandResult>(new TestCommandResult {ResultingValue = 5});
            return Task.FromResult((ICommandWorkflowResult) returnObject);
        }
    }

    public class TestMultipleCommandHandlerWithResult : CommandHandler<TestMultipleCommandWithResult>
    {
        public TestMultipleCommandHandlerWithResult(IMediator mediator) : base(mediator)
        {
        }

        protected override Task Validate(ValidationContext validationContext)
        {
            if (string.Compare(Command.Name, "foo", StringComparison.OrdinalIgnoreCase) == 0)
                validationContext.AddError("UserId", "Validation Failed");
            return Task.FromResult(0);
        }

        protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext)
        {
            var commandOutput = await Mediator.Send(new TestCommandWithResult { Id = Command.Id });

            if (!commandOutput.Success)
                return new CommandWorkflowResult(commandOutput.ValidationResults);

            return new CommandWorkflowResult<TestCommandResult>(commandOutput.Result<TestCommandResult>());
        }
    }

    public class TestCommandHandler : CommandHandler<TestCommand>
    {
        private readonly IMediator _mediator;

        public TestCommandHandler(IMediator mediator)
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

        protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext)
        {
            _mediator.DeferEvent(new FakeEvent());
            _mediator.DeferEvent(new FakeEventFromHandler());
            return Task.FromResult(new CommandWorkflowResult() as ICommandWorkflowResult);
        }
    }
}