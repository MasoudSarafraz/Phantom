using ECommerce.IntegrationContracts;
using ECommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Messaging.Abstractions;

namespace ECommerce.Application.Handlers;

public class OrderPlacedEventHandler : IDomainEventHandler<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedEventHandler> _logger;
    private readonly IEventPublisher _eventPublisher;

    public OrderPlacedEventHandler(ILogger<OrderPlacedEventHandler> logger, IEventPublisher eventPublisher)
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Domain] Order {OrderId} placed. Customer: {CustomerId}, Total: {Amount} {Currency}",
            domainEvent.OrderId, domainEvent.CustomerId, domainEvent.TotalAmount, domainEvent.Currency);

        await _eventPublisher.PublishAsync(new OrderCreatedIntegrationEvent(
            domainEvent.OrderId,
            domainEvent.CustomerId,
            domainEvent.TotalAmount,
            domainEvent.Currency), cancellationToken);
    }
}

public class OrderShippedEventHandler : IDomainEventHandler<OrderShippedEvent>
{
    private readonly ILogger<OrderShippedEventHandler> _logger;
    private readonly IEventPublisher _eventPublisher;

    public OrderShippedEventHandler(ILogger<OrderShippedEventHandler> logger, IEventPublisher eventPublisher)
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(OrderShippedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Domain] Order {OrderId} shipped. Tracking: #{TrackingNumber}",
            domainEvent.OrderId, domainEvent.TrackingNumber);

        await _eventPublisher.PublishAsync(new OrderShippedIntegrationEvent(
            domainEvent.OrderId,
            domainEvent.TrackingNumber), cancellationToken);
    }
}
