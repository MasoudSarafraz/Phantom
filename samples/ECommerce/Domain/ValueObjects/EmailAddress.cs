using Microsoft.EntityFrameworkCore;
using Phantom.Core.Domain;

namespace ECommerce.Domain.ValueObjects;

[Owned]
public class EmailAddress : ValueObject
{
    public string Value { get; private set; } = default!;

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Email is required", nameof(value));
        value = value.Trim().ToLowerInvariant();
        if (!value.Contains('@') || !value.Contains('.')) throw new ArgumentException("Invalid email format", nameof(value));
        Value = value;
    }

    public static implicit operator string(EmailAddress email) => email.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
