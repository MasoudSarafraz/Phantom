namespace Phantom.Core.Events;

/// <summary>
/// Defines an integration event that is published across bounded contexts
/// for inter-service communication. Integration events are typically serialized
/// and transported via message brokers.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the unique identifier for this integration event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the date and time when this integration event occurred.
    /// </summary>
    DateTimeOffset OccurredOn { get; }

    /// <summary>
    /// Gets the name of the event, used for routing and type resolution during deserialization.
    /// </summary>
    string EventName { get; }

    /// <summary>
    /// Gets the correlation identifier for tracing this event across distributed systems,
    /// or <c>null</c> if no correlation context is available.
    /// </summary>
    string? CorrelationId { get; }
}

/// <summary>
/// Abstract base class for integration events. Provides default implementations for
/// common integration event properties.
/// </summary>
public abstract class IntegrationEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public virtual string EventName => GetType().Name;

    /// <inheritdoc />
    public string? CorrelationId { get; set; }
}
