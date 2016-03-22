using System;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
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

    public class TestMultipleCommandWithResult : ICommand
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }
}
