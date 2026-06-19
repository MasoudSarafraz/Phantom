using Phantom.Core.Domain;

namespace ECommerce.Domain.ValueObjects;

/// <summary>
/// Money value object — amount + ISO currency code.
/// The [Owned] EF Core attribute is intentionally NOT applied here — persistence concerns
/// belong in the infrastructure layer (ECommerceDbContext configures ownership via OwnsOne).
/// </summary>
public class Money : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required", nameof(currency));
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other) => Currency != other.Currency
        ? throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}")
        : new Money(Amount + other.Amount, Currency);

    public Money Subtract(Money other) => Currency != other.Currency
        ? throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}")
        : new Money(Amount - other.Amount, Currency);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Math.Round(Amount, 2);
        yield return Currency;
    }
}
