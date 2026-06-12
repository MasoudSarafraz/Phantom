using Phantom.Core.Events;

namespace ECommerce.Domain.Events;

public class OrderPlacedEvent : DomainEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public string Currency { get; }

    public OrderPlacedEvent(Guid orderId, Guid customerId, decimal totalAmount, string currency)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Currency = currency;
    }
}

public class OrderShippedEvent : DomainEvent
{
    public Guid OrderId { get; }
    public string TrackingNumber { get; }

    public OrderShippedEvent(Guid orderId, string trackingNumber)
    {
        OrderId = orderId;
        TrackingNumber = trackingNumber;
    }
}
