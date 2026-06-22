using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.EfCore;

public class DomainEventOutboxDispatcher
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;
    private readonly IMessageSerializer? _messageSerializer;
    private readonly ILogger<DomainEventOutboxDispatcher>? _logger;

    public DomainEventOutboxDispatcher(
        IDomainEventDispatcher? domainEventDispatcher = null,
        IMessageSerializer? messageSerializer = null,
        ILogger<DomainEventOutboxDispatcher>? logger = null)
    {
        _domainEventDispatcher = domainEventDispatcher;
        _messageSerializer = messageSerializer;
        _logger = logger;
    }

    public bool UseOutboxForDomainEvents => _messageSerializer is not null;

    public IReadOnlyList<(IAggregateRoot Aggregate, IReadOnlyList<IDomainEvent> Events)> CollectAndEnqueueOutbox(
        DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var aggregateRoots = dbContext.ChangeTracker.Entries()
            .Where(e => e.Entity is IAggregateRoot)
            .Select(e => (IAggregateRoot)e.Entity)
            .ToList();

        var collected = aggregateRoots
            .Select(ar => (Aggregate: ar, Events: (IReadOnlyList<IDomainEvent>)ar.DomainEvents.ToList()))
            .ToList();

        if (UseOutboxForDomainEvents)
        {
            int totalEvents = 0;
            foreach (var (aggregate, events) in collected)
            {
                foreach (var domainEvent in events)
                {
                    try
                    {
                        var payload = _messageSerializer!.Serialize(domainEvent);
                        var outboxMessage = new OutboxMessage
                        {
                            EventType = domainEvent.GetType().AssemblyQualifiedName!,
                            Payload = System.Text.Encoding.UTF8.GetString(payload),
                            CorrelationId = domainEvent is IIntegrationEvent ie ? ie.CorrelationId : null,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        dbContext.Set<OutboxMessage>().Add(outboxMessage);
                        totalEvents++;
                    }
                    catch (Exception serializeEx)
                    {
                        _logger?.LogError(serializeEx,
                            "[Phantom] Failed to serialize domain event {EventType} for outbox. This event will not be delivered.",
                            domainEvent.GetType().Name);
                    }
                }
            }

            if (totalEvents > 0)
            {
                _logger?.LogDebug("[Phantom] Enqueued {EventCount} domain events to outbox", totalEvents);
            }
        }

        return collected;
    }

    public async Task AfterSaveChangesAsync(
        IReadOnlyList<(IAggregateRoot Aggregate, IReadOnlyList<IDomainEvent> Events)> collected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collected);

        if (!UseOutboxForDomainEvents && _domainEventDispatcher is not null)
        {
            foreach (var (_, events) in collected)
            {
                foreach (var domainEvent in events)
                {
                    try
                    {
                        await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex,
                            "[Phantom] Failed to dispatch domain event {EventType}. Continuing with remaining events.",
                            domainEvent.GetType().Name);
                    }
                }
            }
        }

        foreach (var (aggregate, _) in collected)
        {
            try
            {
                ((IAggregateRootPersistence)aggregate).ClearDomainEvents();
            }
            catch (Exception clearEx)
            {
                _logger?.LogWarning(clearEx,
                    "[Phantom] Failed to clear domain events for aggregate {AggregateType}. Events may be re-dispatched on next save.",
                    aggregate.GetType().Name);
            }
        }
    }
}
