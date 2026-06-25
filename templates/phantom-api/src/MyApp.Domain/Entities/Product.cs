using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace MyApp.Domain.Entities;

public class Product : AuditableAggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsAvailable { get; private set; }

    private Product() { }

    public Product(Guid id, string name, string description, decimal price, int stockQuantity) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));
        if (stockQuantity < 0)
            throw new ArgumentException("Stock cannot be negative.", nameof(stockQuantity));

        Name = name;
        Description = description ?? string.Empty;
        Price = price;
        StockQuantity = stockQuantity;
        IsAvailable = stockQuantity > 0;

        AddDomainEvent(new ProductCreatedEvent(Id, Name, Price));
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(newPrice));
        Price = newPrice;
    }

    public void AdjustStock(int quantity)
    {
        StockQuantity = Math.Max(0, StockQuantity + quantity);
        IsAvailable = StockQuantity > 0;
    }
}

public class ProductCreatedEvent : DomainEvent
{
    public Guid ProductId { get; }
    public string Name { get; }
    public decimal Price { get; }

    public ProductCreatedEvent(Guid productId, string name, decimal price)
    {
        ProductId = productId;
        Name = name;
        Price = price;
    }
}
