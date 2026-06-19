using Phantom.Core.Events;

namespace ECommerce.IntegrationContracts;

/// <summary>
/// Integration event published when a new order is confirmed.
/// Lives in a separate IntegrationContracts project so that both the producing service
/// (ECommerce.Api) and any consuming service can reference the same contract assembly
/// without dragging in domain or application code.
/// </summary>
public class OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public string Currency { get; }

    public OrderCreatedIntegrationEvent(Guid orderId, Guid customerId, decimal totalAmount, string currency)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Currency = currency;
    }
}

public class OrderShippedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; }
    public string TrackingNumber { get; }

    public OrderShippedIntegrationEvent(Guid orderId, string trackingNumber)
    {
        OrderId = orderId;
        TrackingNumber = trackingNumber;
    }
}
