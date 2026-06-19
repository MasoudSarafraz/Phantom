namespace Phantom.CQRS.Commands;

public interface ICommand
{
}

public interface ICommand<TResult> : ICommand
{
}
