namespace Phantom.CQRS.Queries;

/// <summary>
/// Marker interface for a query that returns a result of type <typeparamref name="TResult"/>.
/// Queries represent read operations that do not modify system state.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the query handler.</typeparam>
public interface IQuery<TResult>
{
}
