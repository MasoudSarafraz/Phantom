namespace Phantom.Infrastructure.Abstractions.Outbox;

/// <summary>
/// Represents a domain/integration event that has been persisted to the Outbox table in the same
/// transaction as the aggregate change that produced it. A background <c>OutboxProcessor</c>
/// polls the table, publishes pending messages to the broker, and marks them as published.
///
/// This type lives in <c>Phantom.Infrastructure.Abstractions</c> (not <c>Phantom.Core</c>)
/// because the Outbox is a persistence/infrastructure concern, not a domain concept. Keeping it
/// out of Core ensures that the Domain layer never depends on messaging or persistence concerns.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Name of the default channel used when an outbox message does not target a specific channel.
    /// </summary>
    public const string DefaultChannel = "default";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Assembly-qualified name of the event type. Used by <c>OutboxProcessor</c> to deserialize
    /// the payload back into a strongly-typed event.
    /// </summary>
    public string EventType { get; set; } = default!;

    /// <summary>
    /// Serialized event payload (typically JSON encoded as UTF-8 string).
    /// </summary>
    public string Payload { get; set; } = default!;

    /// <summary>
    /// Channel the event should be published to. Defaults to <see cref="DefaultChannel"/>.
    /// </summary>
    public string Channel { get; set; } = DefaultChannel;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsPublished { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 5;

    public DateTimeOffset? NextRetryAt { get; set; }

    public string? CorrelationId { get; set; }

    public string? LastError { get; set; }
}
