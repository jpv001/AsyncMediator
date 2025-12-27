namespace AsyncMediator;

/// <summary>
/// This is used as a query handler to receive queries from the <see cref="IMediator"/> and return a <see cref="Task{TResult}"/> containing the result.
/// </summary>
/// <typeparam name="TCriteria">The <see cref="Type"/> of criteria object you wish to pass in.</typeparam>
/// <typeparam name="TResult">The <see cref="Type"/> of result that you want to retrieve.</typeparam>
public interface IQuery<in TCriteria, TResult>
{
    /// <summary>
    /// Used to retrieve data using the given criteria object, asynchronously returning the desired results.
    /// </summary>
    /// <param name="criteria">A criteria object that is used to query the data source.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An awaitable task the returns the desired <typeparamref name="TResult"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    Task<TResult> Query(TCriteria criteria, CancellationToken cancellationToken = default);
}
