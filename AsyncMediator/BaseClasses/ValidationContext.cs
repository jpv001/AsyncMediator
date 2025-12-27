using System.ComponentModel.DataAnnotations;

namespace AsyncMediator;

/// <summary>
/// Context for collecting validation errors during command processing.
/// </summary>
/// <remarks>
/// Used by <see cref="CommandHandler{TCommand}"/> to accumulate validation errors
/// during the <see cref="CommandHandler{TCommand}.Validate"/> phase.
/// </remarks>
public class ValidationContext
{
    /// <summary>
    /// Gets or sets the list of validation results accumulated during validation.
    /// </summary>
    public List<ValidationResult> ValidationResults { get; set; } = [];

    /// <summary>
    /// Adds a validation error for a specific member.
    /// </summary>
    /// <param name="memberName">The name of the member that caused the error.</param>
    /// <param name="errorMessage">The error message.</param>
    public void AddError(string memberName, string errorMessage) =>
        ValidationResults.Add(new ValidationResult(errorMessage, [memberName]));

    /// <summary>
    /// Adds multiple validation errors.
    /// </summary>
    /// <param name="errors">The validation results to add.</param>
    public void AddErrors(IEnumerable<ValidationResult> errors) =>
        ValidationResults.AddRange(errors);
}
