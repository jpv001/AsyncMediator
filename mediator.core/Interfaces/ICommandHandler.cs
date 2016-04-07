using System.Threading.Tasks;

namespace AsyncMediator
{
    /// <summary>
    /// Inherit this to handle an <see cref="ICommand"/> or use the provided abstract class <seealso cref="CommandHandler"/>.
    /// </summary>
    /// <typeparam name="TCommand">A <see cref="ICommand"/> that this handler is responsible for processing.</typeparam>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Implement this method to handle a given <see cref="ICommand"/> that has been sent using the <see cref="IMediator"/>.
        /// </summary>
        /// <param name="command">A class that implements <see cref="ICommand"/>.</param>
        /// <returns>A class that implements <see cref="ICommandWorkflowResult"/> that contains <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>
        /// messages and a Success flag.</returns>
        Task<ICommandWorkflowResult> Handle(TCommand command);
    }
}