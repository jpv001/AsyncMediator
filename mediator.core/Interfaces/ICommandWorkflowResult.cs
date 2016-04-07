using System.Collections.Generic;

namespace AsyncMediator
{
    /// <summary>
    /// A base interface that enforces a validation result to be present on <see cref="ICommandHandler{TCommand}"/> results
    /// </summary>
    public interface ICommandWorkflowResult
    {
        /// <summary>
        /// A list of validation errors associated with a <see cref="ICommandWorkflowResult"/>.
        /// </summary>
        IList<ValidationResult> ValidationResults { get; set; }
        
        /// <summary>
        /// A method that checks for success and returns a true for a successful operation.
        /// </summary>
        /// <returns>A boolean that represents the success or failure of the command</returns>
        bool Success { get; }

        TResult Result<TResult>() where TResult : class, new();

        void SetResult<TResult>(TResult result) where TResult : class, new();
    }
}
