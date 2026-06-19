using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Infrastructure.Abstractions.Idempotency;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Decorator that wraps an <see cref="IIntegrationEventHandler{TEvent}"/> with idempotency protection.
/// Before handling an event, it checks <see cref="IIdempotencyTracker"/> to skip already-processed events.
/// After successful handling, it marks the event as processed to prevent duplicate processing.
/// </summary>
public class IdempotentIntegrationEventHandlerDecorator<TEvent> : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly IIntegrationEventHandler<TEvent> _inner;
    private readonly IIdempotencyTracker _tracker;
    private readonly ILogger<IdempotentIntegrationEventHandlerDecorator<TEvent>> _logger;

    public IdempotentIntegrationEventHandlerDecorator(
        IIntegrationEventHandler<TEvent> inner,
        IIdempotencyTracker tracker,
        ILogger<IdempotentIntegrationEventHandlerDecorator<TEvent>> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(TEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (await _tracker.IsProcessedAsync(@event.EventId, ct))
        {
            _logger.LogInformation(
                "[Phantom] Idempotency: Event {EventType} with Id {EventId} already processed. Skipping.",
                @event.EventName, @event.EventId);
            return;
        }

        await _inner.HandleAsync(@event, ct);

        await _tracker.MarkAsProcessedAsync(@event.EventId, @event.EventName, ct);

        _logger.LogDebug(
            "[Phantom] Idempotency: Marked event {EventType} with Id {EventId} as processed.",
            @event.EventName, @event.EventId);
    }
}
