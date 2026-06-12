using ECommerce.Domain.Events;
using Phantom.Core.Events;

namespace ECommerce.Application.Handlers;

public class OrderPlacedEventHandler : IDomainEventHandler<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public OrderPlacedEventHandler(ILogger<OrderPlacedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Order {OrderId} placed. Customer: {CustomerId}, Total: {Amount} {Currency}",
            domainEvent.OrderId, domainEvent.CustomerId, domainEvent.TotalAmount, domainEvent.Currency);
        return Task.CompletedTask;
    }
}

public class OrderShippedEventHandler : IDomainEventHandler<OrderShippedEvent>
{
    private readonly ILogger<OrderShippedEventHandler> _logger;

    public OrderShippedEventHandler(ILogger<OrderShippedEventHandler> logger) => _logger = logger;

    public Task HandleAsync(OrderShippedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Order {OrderId} shipped. Tracking: #{TrackingNumber}",
            domainEvent.OrderId, domainEvent.TrackingNumber);
        return Task.CompletedTask;
    }
}
