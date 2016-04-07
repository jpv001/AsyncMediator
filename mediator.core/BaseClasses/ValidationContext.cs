using System.Collections.Generic;

namespace AsyncMediator
{
    /// <summary>
    /// A context used for validating commands.
    /// Contains a list of <see cref="ValidationResult"/> objects.
    /// </summary>
    public class ValidationContext
    {
        /// <summary>
        /// The validation results.
        /// </summary>
        public List<ValidationResult> ValidationResults { get; set; }

        /// <summary>
        /// Default constructor.
        /// Creates a new <see cref="ValidationContext"/> with no errors.
        /// </summary>
        public ValidationContext()
        {
            ValidationResults = new List<ValidationResult>();
        }

        /// <summary>
        /// Adds a validation error.
        /// </summary>
        /// <param name="memberName">The member that has caused validation to fail.</param>
        /// <param name="errorMessage">The validation error message.</param>
        public void AddError(string errorMessage, string memberName)
        {
            ValidationResults.Add(new ValidationResult(errorMessage, memberName));
        }

        /// <summary>
        /// Adds a number of validation errors.
        /// </summary>
        /// <param name="errors">The list of <see cref="ValidationResult"/> objects.</param>
        public void AddErrors(IEnumerable<ValidationResult> errors)
        {
            ValidationResults.AddRange(errors);
        }
    }
}
