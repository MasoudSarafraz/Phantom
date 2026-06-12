namespace Phantom.CQRS.Pipelines;

/// <summary>
/// Defines a cross-cutting pipeline behavior that wraps request handling.
/// Pipeline behaviors are executed in order around the actual handler,
/// enabling concerns such as validation, logging, caching, and authorization.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
public interface IPipelineBehavior<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Handles the request by performing cross-cutting logic and invoking
    /// the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="next">The delegate for the next action in the pipeline; call this to continue towards the handler.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous pipeline operation.</returns>
    Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}
