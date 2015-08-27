using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace AsyncMediator
{
    /// <summary>
    /// The result of a <see cref="ICommand"/> being handled by the <see cref="IMediator"/> will result in a <see cref="CommandWorkflowResult"/>
    /// containing the <see cref="ValidationResult"/> and a Success flag
    /// </summary>
    /// <typeparam name="T">The type of workflow result</typeparam>
    public class CommandWorkflowResult<T> : CommandWorkflowResult
    {
        public T Result { get; set; }

        public CommandWorkflowResult()
        {
        }

        public CommandWorkflowResult(T result)
        {
            Result = result;
        }
    }

    /// <summary>
    /// The result of a <see cref="ICommand"/> being handled by the <see cref="IMediator"/> will result in a <see cref="CommandWorkflowResult"/>
    /// containing the <see cref="ValidationResult"/> and a Success flag
    /// </summary>
    public class CommandWorkflowResult
    {
        /// <summary>
        /// This returns a <see cref="CommandWorkflowResult"/> that contains the result of executing a validation action passed into it
        /// </summary>
        /// <param name="validationCriteria">A Func containing the predicate for verifying validity</param>
        /// <param name="validAction">An Action that contains the validation logic</param>
        /// <param name="validationError">The error to be thrown in the event of failure</param>
        /// <param name="existingResult">The existing validation errors, if any</param>
        /// <returns><see cref="CommandWorkflowResult"/></returns>
        public static CommandWorkflowResult ValidatedAction(Func<bool> validationCriteria, Action validAction,
            string validationError, CommandWorkflowResult existingResult = null)
        {
            existingResult = existingResult ?? new CommandWorkflowResult();
            
            if (validationCriteria())
                validAction();
            else
                existingResult.AddError(validationError);
            
            return existingResult;
        }

        public CommandWorkflowResult()
        {
            ValidationResults = new List<ValidationResult>();
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> with a specified <see cref="ValidationResult"/> message
        /// </summary>
        /// <param name="errorMessage">An error that has occurred in validation</param>
        public CommandWorkflowResult(string errorMessage)
            : this()
        {
            AddError(errorMessage);
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> with a specified <see cref="ValidationResult"/> message for a given member 
        /// </summary>
        /// <param name="memberName">The member that triggered the validation error</param>
        /// <param name="errorMessage">An error that has occurred in validation</param>
        public CommandWorkflowResult(string memberName, string errorMessage)
            : this()
        {
            AddError(memberName, errorMessage);
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> with a specified list of <see cref="ValidationResult"/> messages
        /// </summary>
        /// <param name="validationResults">A set of <see cref="ValidationResult"/> that represent validation errors</param>
        public CommandWorkflowResult(IEnumerable<ValidationResult> validationResults)
        {
            ValidationResults = validationResults.ToList();
        }

        /// <summary>
        /// A list of validation errors associated with this <see cref="CommandWorkflowResult"/>
        /// </summary>
        public IList<ValidationResult> ValidationResults { get; set; }

        /// <summary>
        /// A flag incidating that there are no validation errors associated with this <see cref="CommandWorkflowResult"/>
        /// </summary>
        public bool Success
        {
            get { return ValidationResults == null || !ValidationResults.Any(); }
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> that has no validation errors - Used when returning successfully from a <see cref="ICommandHandler{TCommand}"/>
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Prefer using static factory methods than using the constructors. At some point we need to make
        /// ctors private and use these factory methods in the code consistently. They read better.
        /// </remarks>
        public static CommandWorkflowResult Ok()
        {
            return new CommandWorkflowResult();
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> that has a validation error associated with a given member        
        /// </summary>
        /// <param name="memberName">The name of the component or property that has failed</param>
        /// <param name="message">The error message that is used to identify the problem</param>
        /// <returns>An <see cref="CommandWorkflowResult"/> with a validation error</returns>
        public static CommandWorkflowResult WithError(string memberName, string message)
        {
            return new CommandWorkflowResult(memberName, message);
        }

        /// <summary>
        /// Creates a <see cref="CommandWorkflowResult"/> with a set of validation errors              
        /// </summary>
        /// <param name="validationResults"></param>
        /// <returns>An <see cref="CommandWorkflowResult"/> with a set of validation errors</returns>
        public static CommandWorkflowResult WithError(IEnumerable<ValidationResult> validationResults)
        {
            return new CommandWorkflowResult(validationResults);
        }

        /// <summary>
        /// This adds a validation error to an existing <see cref="CommandWorkflowResult"/>
        /// </summary>
        /// <param name="message">The validation error message</param>
        public void AddError(string message)
        {
            ValidationResults.Add(new ValidationResult(message));
        }

        /// <summary>
        /// This adds a validation error, for a given member, to an existing <see cref="CommandWorkflowResult"/>
        /// </summary>
        /// <param name="memberName">The name of the member or property that generated the error</param>
        /// <param name="message">The validation error meesage</param>
        public void AddError(string memberName, string message)
        {
            ValidationResults.Add(new ValidationResult(message, new[] { memberName }));
        }

        /// <summary>
        /// This adds a validation error to an existing <see cref="CommandWorkflowResult"/>        
        /// </summary>
        /// <param name="validationResult">The validation error</param>
        public void AddError(ValidationResult validationResult)
        {
            ValidationResults.Add(validationResult);
        }

        public override bool Equals(object obj)
        {
            var other = obj as CommandWorkflowResult;
            return other != null && Equals(other);
        }

        protected bool Equals(CommandWorkflowResult other)
        {
            return ValidationResults.SequenceEqual(other.ValidationResults);
        }

        public override int GetHashCode()
        {
            return (ValidationResults != null ? ValidationResults.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return String.Join("\n", ValidationResults.Select(vr => vr.ErrorMessage));
        }
    }
}
