namespace Phantom.CQRS.Exceptions;

/// <summary>
/// Thrown when no handler could be resolved from the DI container for a given request type.
/// Ensure the handler is registered via <c>AddPhantomCQRS</c> or manually in the service collection.
/// </summary>
public class HandlerNotFoundException : Exception
{
    /// <summary>
    /// Gets the request type for which no handler was found.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerNotFoundException"/> class.
    /// </summary>
    /// <param name="requestType">The type of request that had no registered handler.</param>
    public HandlerNotFoundException(Type requestType)
        : base($"No handler could be resolved for request type '{requestType.FullName}'. Ensure the handler is registered in the DI container.")
    {
        RequestType = requestType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerNotFoundException"/> class
    /// with a custom error message.
    /// </summary>
    /// <param name="requestType">The type of request that had no registered handler.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public HandlerNotFoundException(Type requestType, string message)
        : base(message)
    {
        RequestType = requestType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerNotFoundException"/> class
    /// with a custom error message and an inner exception.
    /// </summary>
    /// <param name="requestType">The type of request that had no registered handler.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public HandlerNotFoundException(Type requestType, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestType = requestType;
    }
}
