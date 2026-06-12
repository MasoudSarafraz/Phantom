namespace Phantom.CQRS.Commands;

/// <summary>
/// Marker interface for a command that does not return a result.
/// Commands represent write operations that change system state.
/// </summary>
public interface ICommand
{
}

/// <summary>
/// Marker interface for a command that returns a result of type <typeparamref name="TResult"/>.
/// Commands represent write operations that change system state and may return
/// a value (e.g. a created entity identifier).
/// </summary>
/// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
public interface ICommand<TResult> : ICommand
{
}
