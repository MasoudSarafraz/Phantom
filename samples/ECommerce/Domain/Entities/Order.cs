using ECommerce.Domain.Events;
using ECommerce.Domain.ValueObjects;
using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace ECommerce.Domain.Entities;

public class Order : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public string Status { get; private set; } = default!;
    public string ShippingAddress { get; private set; } = default!;
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order() { }

    public Order(Guid id, Guid customerId, string shippingAddress) : base(id)
    {
        CustomerId = customerId;
        ShippingAddress = shippingAddress ?? throw new ArgumentNullException(nameof(shippingAddress));
        Status = "Pending";
    }

    public void AddLine(OrderLine line)
    {
        if (Status != "Pending") throw new InvalidOperationException("Cannot modify a confirmed order");
        _lines.Add(line ?? throw new ArgumentNullException(nameof(line)));
    }

    public void Confirm()
    {
        if (Status != "Pending") throw new InvalidOperationException("Only pending orders can be confirmed");
        if (!_lines.Any()) throw new InvalidOperationException("Cannot confirm an empty order");
        Status = "Confirmed";
        var total = CalculateTotal();
        AddDomainEvent(new OrderPlacedEvent(Id, CustomerId, total.Amount, total.Currency));
    }

    public void Ship(string trackingNumber)
    {
        if (Status != "Confirmed") throw new InvalidOperationException("Only confirmed orders can be shipped");
        Status = "Shipped";
        AddDomainEvent(new OrderShippedEvent(Id, trackingNumber));
    }

    public void Cancel()
    {
        if (Status is "Shipped" or "Delivered") throw new InvalidOperationException("Cannot cancel a shipped/delivered order");
        Status = "Cancelled";
    }

    public Money CalculateTotal()
    {
        if (!_lines.Any()) return new Money(0, "USD");
        return _lines.Select(l => l.LineTotal).Aggregate((a, b) => a.Add(b));
    }
}
