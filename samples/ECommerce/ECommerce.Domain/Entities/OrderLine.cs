using ECommerce.Domain.ValueObjects;
using Phantom.Core.Domain;

namespace ECommerce.Domain.Entities;

public class OrderLine : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = default!;

    private OrderLine() { }

    public OrderLine(Guid productId, string productName, int quantity, Money unitPrice) : base(Guid.NewGuid())
    {
        ProductId = productId;
        ProductName = productName ?? throw new ArgumentNullException(nameof(productName));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be positive");
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
    }

    public Money LineTotal => new(UnitPrice.Amount * Quantity, UnitPrice.Currency);
}
