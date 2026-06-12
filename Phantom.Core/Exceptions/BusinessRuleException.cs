namespace Phantom.Core.Exceptions;

public class BusinessRuleException : DomainException
{
    public string? Code { get; }

    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }

    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    public BusinessRuleException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}
