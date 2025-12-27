using System.ComponentModel.DataAnnotations;

namespace AsyncMediator;

/// <summary>
/// Generic command result with a typed result value.
/// </summary>
/// <typeparam name="TResult">The type of the result value.</typeparam>
public class CommandWorkflowResult<TResult> : CommandWorkflowResult
    where TResult : class, new()
{
    /// <summary>
    /// Initializes a new successful command result with the specified result value.
    /// </summary>
    /// <param name="result">The result value.</param>
    public CommandWorkflowResult(TResult result) => SetResult(result);

    /// <summary>
    /// Initializes a new command result with a result value and validation results.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="validationResults">The validation results.</param>
    public CommandWorkflowResult(TResult result, List<ValidationResult> validationResults)
    {
        SetResult(result);
        ValidationResults = validationResults;
    }

    /// <summary>
    /// Initializes a new command result with validation errors (no result value).
    /// </summary>
    /// <param name="validationResults">The validation results containing errors.</param>
    public CommandWorkflowResult(IEnumerable<ValidationResult> validationResults)
        : base(validationResults)
    { }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is CommandWorkflowResult<TResult> other &&
        ResultEquals(other.ObjectResult as TResult) &&
        base.Equals(other);

    /// <summary>
    /// Determines whether the specified result value is equal to this result value.
    /// </summary>
    /// <param name="otherResult">The result value to compare.</param>
    /// <returns>True if the result values are equal.</returns>
    protected bool ResultEquals(TResult? otherResult) =>
        ObjectResult?.Equals(otherResult) ?? otherResult is null;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ObjectResult);
}
