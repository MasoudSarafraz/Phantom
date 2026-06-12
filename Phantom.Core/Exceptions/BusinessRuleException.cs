namespace Phantom.Core.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated within the domain.
/// Optionally carries a machine-readable error code for programmatic handling.
/// </summary>
public class BusinessRuleException : DomainException
{
    /// <summary>
    /// Gets the machine-readable error code associated with this business rule violation,
    /// or <c>null</c> if no code is specified.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleException"/> class with an error message.
    /// </summary>
    /// <param name="message">A description of the business rule violation.</param>
    public BusinessRuleException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleException"/> class with an error message and an inner exception.
    /// </summary>
    /// <param name="message">A description of the business rule violation.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleException"/> class with an error code and message.
    /// </summary>
    /// <param name="code">A machine-readable error code for programmatic handling.</param>
    /// <param name="message">A description of the business rule violation.</param>
    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleException"/> class with an error code, message, and inner exception.
    /// </summary>
    /// <param name="code">A machine-readable error code for programmatic handling.</param>
    /// <param name="message">A description of the business rule violation.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public BusinessRuleException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}
