namespace Phantom.CQRS.Queries;

/// <summary>
/// Defines a handler for a query that returns a result.
/// Implementations are resolved from the DI container by the <see cref="Dispatchers.IDispatcher"/>.
/// </summary>
/// <typeparam name="TQuery">The type of query this handler processes.</typeparam>
/// <typeparam name="TResult">The type of result returned by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the specified query asynchronously and returns a result.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous handle operation, containing the result.</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
