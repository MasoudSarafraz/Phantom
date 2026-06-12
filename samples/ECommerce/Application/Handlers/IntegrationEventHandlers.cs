using ECommerce.Application.IntegrationEvents;
using Phantom.Core.Messaging;

namespace ECommerce.Application.Handlers;

public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private readonly ILogger<OrderCreatedIntegrationEventHandler> _logger;

    public OrderCreatedIntegrationEventHandler(ILogger<OrderCreatedIntegrationEventHandler> logger) => _logger = logger;

    public Task HandleAsync(OrderCreatedIntegrationEvent e, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Integration] Order {OrderId} created for customer {CustomerId}",
            e.OrderId, e.CustomerId);
        return Task.CompletedTask;
    }
}

public class OrderShippedIntegrationEventHandler : IIntegrationEventHandler<OrderShippedIntegrationEvent>
{
    private readonly ILogger<OrderShippedIntegrationEventHandler> _logger;

    public OrderShippedIntegrationEventHandler(ILogger<OrderShippedIntegrationEventHandler> logger) => _logger = logger;

    public Task HandleAsync(OrderShippedIntegrationEvent e, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Integration] Order {OrderId} shipped. Tracking: {Tracking}",
            e.OrderId, e.TrackingNumber);
        return Task.CompletedTask;
    }
}
