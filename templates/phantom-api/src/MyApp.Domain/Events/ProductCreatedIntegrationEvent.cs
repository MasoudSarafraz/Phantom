using Phantom.Core.Events;

namespace MyApp.Domain.Events;

public class ProductCreatedIntegrationEvent : IntegrationEvent
{
    public Guid ProductId { get; }
    public string Name { get; }
    public decimal Price { get; }

    public ProductCreatedIntegrationEvent(Guid productId, string name, decimal price)
    {
        ProductId = productId;
        Name = name;
        Price = price;
    }
}
