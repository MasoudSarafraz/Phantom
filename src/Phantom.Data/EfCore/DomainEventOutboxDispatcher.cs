using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.EfCore;

/// <summary>
/// Encapsulates the domain-event → outbox-message pipeline that was previously inlined
/// inside <c>PhantomDbContext.SaveChangesAsync</c>.
///
/// Splitting this logic into a dedicated type has three benefits:
///   1. The DbContext returns to being a thin persistence concern instead of a dispatcher.
///   2. The pipeline is unit-testable in isolation without a real DbContext.
///   3. Downstream applications can subclass or replace the dispatcher without touching the DbContext.
///
/// Two operating modes are supported, mirroring the legacy behavior:
///   - Outbox mode (when an <see cref="IMessageSerializer"/> is registered):
///     domain events are serialized into <see cref="OutboxMessage"/> rows that are saved
///     in the same transaction as the aggregate change. An <see cref="IDomainEventDispatcher"/>
///     is NOT called here — the OutboxProcessor publishes the messages asynchronously.
///   - In-process mode (no serializer registered):
///     domain events are dispatched in-process via <see cref="IDomainEventDispatcher"/>
///     AFTER the SaveChanges call returns. This is the legacy "no outbox" behavior.
/// </summary>
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

    /// <summary>
    /// True when an <see cref="IMessageSerializer"/> is registered, meaning domain events
    /// should be persisted to the outbox table instead of dispatched in-process.
    /// </summary>
    public bool UseOutboxForDomainEvents => _messageSerializer is not null;

    /// <summary>
    /// Inspects the ChangeTracker for aggregate roots, collects their domain events,
    /// and (in outbox mode) writes corresponding <see cref="OutboxMessage"/> rows into
    /// the supplied DbContext so they are persisted in the same SaveChanges transaction.
    ///
    /// Returns the list of collected (aggregate, events) pairs so the caller can clear
    /// the events from the aggregates after the SaveChanges has succeeded.
    /// </summary>
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
                    var payload = _messageSerializer!.Serialize(domainEvent);
                    var outboxMessage = new OutboxMessage
                    {
                        EventType = domainEvent.GetType().AssemblyQualifiedName!,
                        Payload = System.Text.Encoding.UTF8.GetString(payload),
                        CorrelationId = domainEvent is IIntegrationEvent ie ? ie.CorrelationId : null
                    };
                    dbContext.Set<OutboxMessage>().Add(outboxMessage);
                    totalEvents++;
                }
            }

            if (totalEvents > 0)
            {
                _logger?.LogDebug("[Phantom] Enqueued {EventCount} domain events to outbox", totalEvents);
            }
        }

        return collected;
    }

    /// <summary>
    /// After SaveChanges has succeeded, either:
    ///   - In outbox mode: clear domain events from the aggregates (they are now safely
    ///     persisted in the outbox table and will be published by the OutboxProcessor).
    ///   - In in-process mode: dispatch each domain event via <see cref="IDomainEventDispatcher"/>,
    ///     swallowing per-event failures so a single bad handler does not roll back the
    ///     transaction. Then clear events.
    /// </summary>
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

        // Always clear events from aggregates — they have either been safely persisted to the
        // outbox (outbox mode) or dispatched to in-process handlers (in-process mode).
        foreach (var (aggregate, _) in collected)
        {
            ((IAggregateRootPersistence)aggregate).ClearDomainEvents();
        }
    }
}
