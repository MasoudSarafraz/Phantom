namespace Phantom.Core.Events;

/// <summary>
/// Defines a domain event that represents something that has happened within the domain.
/// Domain events are raised by aggregates and dispatched to event handlers for processing.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier for this domain event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the date and time when this domain event occurred.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// Abstract base class for domain events. Provides default implementations for
/// <see cref="EventId"/> and <see cref="OccurredOn"/>.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
