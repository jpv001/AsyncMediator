using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AsyncMediator
{
    /// <summary>
    /// A generic version of the <see cref="CommandWorkflowResult"/> class, including a workflow result object.
    /// </summary>
    /// <typeparam name="TResult">The type of workflow result</typeparam>
    public class CommandWorkflowResult<TResult> : CommandWorkflowResult
        where TResult : class, new()
    {
        /// <summary>
        /// Default constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/>.
        /// </summary>
        public CommandWorkflowResult()
        {
        }

        /// <summary>
        /// Public constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/> with a Result.
        /// </summary>
        /// <param name="result">The result instance.</param>
        public CommandWorkflowResult(TResult result)
        {
            SetResult(result);
        }

        /// <summary>
        /// Public constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/> with a Result.
        /// </summary>
        /// <param name="result">The result instance.</param>
        /// <param name="validationResults">Existing validation results that you want to create the <see cref="CommandWorkflowResult{T}"/> with.</param>
        public CommandWorkflowResult(TResult result, IList<ValidationResult> validationResults)
        {
            SetResult(result);
            ValidationResults = validationResults;
        }

        /// <summary>
        /// Public constructor.
        /// Creates a new <see cref="CommandWorkflowResult{T}"/> with a Result.
        /// </summary>
        /// <param name="validationResults">Existing validation results that you want to create the <see cref="CommandWorkflowResult{T}"/> with.</param>
        public CommandWorkflowResult(IEnumerable<ValidationResult> validationResults)
            : base(validationResults)
        { }

        /// <summary>
        /// Determines whether the specified object is equal to this object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if they are equal; false if not.</returns>
        public override bool Equals(object obj)
        {
            var other = obj as CommandWorkflowResult<TResult>;
            return other != null && ResultEquals(other.ObjectResult as TResult) && base.Equals(other);
        }

        /// <summary>
        /// Determines whether the Result object is equal to the other Result object.
        /// </summary>
        /// <param name="otherResult">The other result.</param>
        /// <returns>True if they are equal; false if not.</returns>
        protected bool ResultEquals(TResult otherResult)
        {
            return (ObjectResult == null) ? (otherResult == null) : ObjectResult.Equals(otherResult);
        }
    }
}
