using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;

namespace Phantom.CQRS.Dispatchers;

/// <summary>
/// Abstraction for dispatching commands and queries to their respective handlers,
/// with support for pipeline behaviors (validation, logging, etc.).
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Dispatches a command that does not return a result to its registered handler.
    /// Pipeline behaviors configured for the command type will be invoked in order.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous dispatch operation.</returns>
    /// <exception cref="Phantom.CQRS.Exceptions.HandlerNotFoundException">
    /// Thrown when no handler is registered for the given command type.
    /// </exception>
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;

    /// <summary>
    /// Dispatches a command that returns a result to its registered handler.
    /// Pipeline behaviors configured for the command type will be invoked in order.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous dispatch operation, containing the handler result.</returns>
    /// <exception cref="Phantom.CQRS.Exceptions.HandlerNotFoundException">
    /// Thrown when no handler is registered for the given command and result type.
    /// </exception>
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a query to its registered handler and returns the result.
    /// Pipeline behaviors configured for the query type will be invoked in order.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the query handler.</typeparam>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous dispatch operation, containing the handler result.</returns>
    /// <exception cref="Phantom.CQRS.Exceptions.HandlerNotFoundException">
    /// Thrown when no handler is registered for the given query and result type.
    /// </exception>
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
