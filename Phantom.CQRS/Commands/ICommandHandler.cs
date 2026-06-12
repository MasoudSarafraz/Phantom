namespace Phantom.CQRS.Commands;

/// <summary>
/// Defines a handler for a command that does not return a result.
/// Implementations are resolved from the DI container by the <see cref="Dispatchers.IDispatcher"/>.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes.</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles the specified command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous handle operation.</returns>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for a command that returns a result.
/// Implementations are resolved from the DI container by the <see cref="Dispatchers.IDispatcher"/>.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes.</typeparam>
/// <typeparam name="TResult">The type of result returned by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the specified command asynchronously and returns a result.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous handle operation, containing the result.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
