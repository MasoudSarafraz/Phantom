namespace Phantom.CQRS.Exceptions;

public class HandlerNotFoundException : Exception
{
    public Type RequestType { get; }

    public HandlerNotFoundException(Type requestType)
        : base($"No handler could be resolved for request type '{requestType.FullName}'. Ensure the handler is registered in the DI container.")
    {
        RequestType = requestType;
    }

    public HandlerNotFoundException(Type requestType, string message)
        : base(message)
    {
        RequestType = requestType;
    }

    public HandlerNotFoundException(Type requestType, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestType = requestType;
    }
}
