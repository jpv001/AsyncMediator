using System.ComponentModel.DataAnnotations;

namespace AsyncMediator;

/// <summary>
/// Result of a command execution containing validation results and optional return value.
/// </summary>
/// <remarks>
/// Uses a static singleton for the success case to eliminate allocations on the hot path.
/// When errors are added, a mutable list is created on-demand.
/// </remarks>
public class CommandWorkflowResult : ICommandWorkflowResult
{
    private static readonly List<ValidationResult> EmptyValidationResults = [];
    private List<ValidationResult> _validationResults = EmptyValidationResults;

    /// <summary>
    /// Initializes a new successful command result with no validation errors.
    /// </summary>
    public CommandWorkflowResult() { }

    /// <summary>
    /// Initializes a new command result with a single error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    public CommandWorkflowResult(string errorMessage) : this() => AddError(errorMessage);

    /// <summary>
    /// Initializes a new command result with a single error for a specific member.
    /// </summary>
    /// <param name="memberName">The name of the member that caused the error.</param>
    /// <param name="errorMessage">The error message.</param>
    public CommandWorkflowResult(string memberName, string errorMessage) : this() =>
        AddError(memberName, errorMessage);

    /// <summary>
    /// Initializes a new command result with validation errors.
    /// </summary>
    /// <param name="validationResults">The validation results containing errors.</param>
    public CommandWorkflowResult(IEnumerable<ValidationResult> validationResults) =>
        _validationResults = validationResults is List<ValidationResult> list
            ? list
            : validationResults.ToList();

    /// <summary>
    /// Gets the list of validation errors. Empty list indicates success.
    /// Prefer using <see cref="AddError(string)"/> methods over direct list mutation.
    /// </summary>
    public List<ValidationResult> ValidationResults
    {
        get
        {
            EnsureMutableList();
            return _validationResults;
        }
        protected set => _validationResults = value;
    }

    /// <summary>
    /// Gets a value indicating whether the command succeeded (no validation errors).
    /// </summary>
    public bool Success => _validationResults.Count == 0;

    /// <summary>
    /// Gets the optional result object.
    /// </summary>
    protected object? ObjectResult { get; private set; }

    /// <summary>
    /// Gets the typed result value, or null if not set or wrong type.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <returns>The result cast to the specified type, or null.</returns>
    public TResult? Result<TResult>() where TResult : class => ObjectResult as TResult;

    /// <summary>
    /// Sets the result value.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result value.</param>
    public void SetResult<TResult>(TResult result) where TResult : class => ObjectResult = result;

    /// <summary>
    /// Creates a successful command result with no errors.
    /// </summary>
    /// <returns>A successful <see cref="CommandWorkflowResult"/>.</returns>
    public static CommandWorkflowResult Ok() => new();

    /// <summary>
    /// Creates a command result with a single validation error.
    /// </summary>
    /// <param name="memberName">The name of the member that caused the error.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A <see cref="CommandWorkflowResult"/> with the specified error.</returns>
    public static CommandWorkflowResult WithError(string memberName, string message) =>
        new(memberName, message);

    /// <summary>
    /// Creates a command result with validation errors.
    /// </summary>
    /// <param name="validationResults">The validation results containing errors.</param>
    /// <returns>A <see cref="CommandWorkflowResult"/> with the specified errors.</returns>
    public static CommandWorkflowResult WithError(IEnumerable<ValidationResult> validationResults) =>
        new(validationResults);

    /// <summary>
    /// Adds an error message to the validation results.
    /// </summary>
    /// <param name="message">The error message.</param>
    public void AddError(string message)
    {
        EnsureMutableList();
        ValidationResults.Add(new ValidationResult(message));
    }

    /// <summary>
    /// Adds an error for a specific member to the validation results.
    /// </summary>
    /// <param name="memberName">The name of the member that caused the error.</param>
    /// <param name="message">The error message.</param>
    public void AddError(string memberName, string message)
    {
        EnsureMutableList();
        ValidationResults.Add(new ValidationResult(message, [memberName]));
    }

    /// <summary>
    /// Adds a validation result to the error collection.
    /// </summary>
    /// <param name="validationResult">The validation result to add.</param>
    public void AddError(ValidationResult validationResult)
    {
        EnsureMutableList();
        ValidationResults.Add(validationResult);
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Join(Environment.NewLine, ValidationResults.Select(vr => vr.ErrorMessage));

    private void EnsureMutableList()
    {
        if (ReferenceEquals(_validationResults, EmptyValidationResults))
            _validationResults = [];
    }
}
