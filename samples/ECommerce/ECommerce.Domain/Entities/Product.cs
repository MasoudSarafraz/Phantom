using ECommerce.Domain.ValueObjects;
using Phantom.Core.Domain;

namespace ECommerce.Domain.Entities;

public class Product : AuditableEntity<Guid>
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public int StockQuantity { get; private set; }
    public bool IsAvailable { get; private set; }

    private Product() { }

    public Product(Guid id, string name, string description, Money price, int stockQuantity) : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        Price = price ?? throw new ArgumentNullException(nameof(price));
        StockQuantity = stockQuantity >= 0 ? stockQuantity : throw new ArgumentException("Stock cannot be negative");
        IsAvailable = stockQuantity > 0;
    }

    public void UpdatePrice(Money newPrice) => Price = newPrice ?? throw new ArgumentNullException(nameof(newPrice));

    public void AdjustStock(int quantity)
    {
        StockQuantity = Math.Max(0, StockQuantity + quantity);
        IsAvailable = StockQuantity > 0;
    }
}
