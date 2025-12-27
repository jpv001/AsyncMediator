namespace AsyncMediator;

/// <summary>
/// Delegate representing the next handler/behavior in the pipeline.
/// Call this to continue execution to the next behavior or final handler.
/// </summary>
/// <typeparam name="TResponse">The response type from the handler.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Pipeline behavior for cross-cutting concerns.
/// Implement to intercept commands, queries, or events before/after handler execution.
/// </summary>
/// <remarks>
/// <para>
/// Behaviors execute in registration order, wrapping around the handler like middleware.
/// Each behavior receives a <c>next</c> delegate to invoke the next behavior
/// or the final handler. Behaviors can:
/// </para>
/// <list type="bullet">
///   <item>Execute logic before calling <c>next</c></item>
///   <item>Execute logic after calling <c>next</c></item>
///   <item>Short-circuit by not calling <c>next</c> and returning a response directly</item>
///   <item>Catch and handle exceptions from the inner pipeline</item>
/// </list>
/// <para>
/// Example implementation:
/// <code>
/// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
/// {
///     public async Task&lt;TResponse&gt; Handle(TRequest request, RequestHandlerDelegate&lt;TResponse&gt; next, CancellationToken ct)
///     {
///         Console.WriteLine($"Handling {typeof(TRequest).Name}");
///         var response = await next();
///         Console.WriteLine($"Handled {typeof(TRequest).Name}");
///         return response;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type (command, query criteria, etc.).</typeparam>
/// <typeparam name="TResponse">The response type from the handler.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the request and optionally invokes the next behavior in the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler. Must be called to continue the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The response from the pipeline.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
