using System.ComponentModel.DataAnnotations;

namespace AsyncMediator;

/// <summary>
/// Result of a command execution containing validation results and success status.
/// </summary>
public interface ICommandWorkflowResult
{
    /// <summary>
    /// Validation errors from command execution. Empty list indicates success.
    /// </summary>
    List<ValidationResult> ValidationResults { get; }

    /// <summary>
    /// True when no validation errors exist.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the typed result, or null if validation failed.
    /// </summary>
    TResult? Result<TResult>() where TResult : class;

    /// <summary>
    /// Sets the command result.
    /// </summary>
    void SetResult<TResult>(TResult result) where TResult : class;
}
