using System.Threading.Tasks;

namespace AsyncMediator.Test
{
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
}