namespace AsyncMediator;

/// <summary>
/// This is used as a query handler to receive queries from the <see cref="IMediator"/>, that do not require criteria,
/// and returns a <see cref="Task{TResult}"/> containing the result.
/// </summary>
/// <typeparam name="TResult">The <see cref="Type"/> of result that you want to retrieve.</typeparam>
public interface ILookupQuery<TResult>
{
    /// <summary>
    /// Performs a lookup query, returns results.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> containing the results.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<TResult> Query(CancellationToken cancellationToken = default);
}
