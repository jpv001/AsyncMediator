using System.Threading.Tasks;

namespace AsyncMediator.Test
{
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

        protected override Task DoHandle(ValidationContext validationContext)
        {
            _mediator.DeferEvent(new FakeEvent());
            _mediator.DeferEvent(new FakeEventFromHandler());
            return Task.FromResult(0);
        }
    }
}