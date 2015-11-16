using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace AsyncMediator
{
    /// <summary>
    /// An abstract base class for a command handler.
    /// </summary>
    /// <typeparam name="TCommand">A <see cref="ICommand"/> that this handler is responsible for processing.</typeparam>
    /// <remarks>
    /// This is more of an example of how a command handler looks - you may wish to directly implement <see cref="ICommandHandler{TCommand}"/> 
    /// to support your own workflow and transactional boundaries.
    /// </remarks>
    public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// The command instance.
        /// </summary>
        protected TCommand Command { get; private set; }

        /// <summary>
        /// The mediator instance.
        /// </summary>
        protected IMediator Mediator { get; private set; }

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="mediator">The <see cref="IMediator"/> instance.</param>
        protected CommandHandler(IMediator mediator)
        {
            Mediator = mediator;
        }

        /// <summary>
        /// Handles the given <see cref="ICommand"/> that has been sent using the <see cref="IMediator"/>.
        /// </summary>
        /// <param name="command">A class that implements <see cref="ICommand"/>.</param>
        /// <returns>A <see cref="CommandWorkflowResult"/> that contains <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>
        /// messages and a Success flag.</returns>
        public async Task<ICommandWorkflowResult> Handle(TCommand command)
        {
            // Save the command instance - the command may be used by the Validate and DoHandle methods.
            Command = command;

            var context = new ValidationContext();
            ICommandWorkflowResult workflowResult;

            // Perform validation.
            await Validate(context);
            if (context.ValidationResults.Any())
            {
                return new CommandWorkflowResult(context.ValidationResults);
            }

            // Handle the event, and execute any deferred events that have been raised via the mediator.
            // Making sure that both the commands and events are wrapped up within a single transaction.
            // The command and related events are all async, so use TransactionScopeAsyncFlowOption.Enabled.
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                workflowResult = await DoHandle(context);

                if (context.ValidationResults.Any())
                {
                    return new CommandWorkflowResult(context.ValidationResults);
                }

                await Mediator.ExecuteDeferredEvents();

                transaction.Complete();
            }

            return workflowResult ?? new CommandWorkflowResult();
        }

        /// <summary>
        /// Performs any necessary validation.
        /// Override in the child class.
        /// </summary>
        /// <param name="validationContext">The validation context</param>
        /// <returns>An async task for validating the command.</returns>
        /// <remarks>
        /// This method should interrogate the Command and add any validation errors to the validation context supplied.
        /// </remarks>
        protected abstract Task Validate(ValidationContext validationContext);

        /// <summary>
        /// Handles the command.
        /// Override in the child class.
        /// </summary>
        /// <param name="validationContext">The validation context</param>
        /// <returns>An async task of type <see cref="ICommandWorkflowResult"/> for handling the result.</returns>
        /// <remarks>
        /// Any errors that occur when handling the Command should be added to the validation context supplied.
        /// </remarks>
        protected abstract Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext);
    }
}
