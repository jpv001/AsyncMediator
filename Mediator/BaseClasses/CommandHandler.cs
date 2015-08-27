using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace AsyncMediator
{
    /// <summary>
    /// This is more of an example of how a command handler looks, usually you'll directly implement <see cref="ICommandHandler{TCommand}"/> 
    /// to support your workflow and transactional boundaries
    /// </summary>
    /// <typeparam name="TCommand"></typeparam>
    public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        protected TCommand Command { get; set; }
        protected readonly IMediator Mediator;
        protected CommandHandler(IMediator mediator)
        {
            Mediator = mediator;
        }

        public async Task<CommandWorkflowResult> Handle(TCommand command)
        {
            Command = command;

            var context = new ValidationContext();

            await Validate(context);
            if (context.ValidationResults.Any()) return new CommandWorkflowResult(context.ValidationResults);

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await DoHandle(context);

                if (context.ValidationResults.Any()) return new CommandWorkflowResult(context.ValidationResults);

                await Mediator.ExecuteDeferredEvents();

                transaction.Complete();
            }

            return new CommandWorkflowResult();
        }

        protected abstract Task Validate(ValidationContext validationContext);

        protected abstract Task DoHandle(ValidationContext validationContext);

    }
}
