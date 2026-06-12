namespace Phantom.Messaging.Outbox;

/// <summary>
/// Represents a message stored in the outbox, awaiting publication to a messaging channel.
/// Implements the transactional outbox pattern to guarantee at-least-once delivery.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Default channel name used when no specific channel is specified.
    /// </summary>
    public const string DefaultChannel = "default";

    /// <summary>
    /// Gets or sets the unique identifier for this outbox message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the assembly-qualified name of the event type, used for runtime type resolution during deserialization.
    /// </summary>
    public string EventType { get; set; } = default!;

    /// <summary>
    /// Gets or sets the serialized event payload.
    /// </summary>
    public string Payload { get; set; } = default!;

    /// <summary>
    /// Gets or sets the name of the channel to publish this message to.
    /// Defaults to <see cref="DefaultChannel"/> when no specific channel is specified.
    /// </summary>
    public string Channel { get; set; } = DefaultChannel;

    /// <summary>
    /// Gets or sets the date and time when this outbox message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when this message was successfully published, or null if not yet published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this message has been successfully published.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Gets or sets the number of times publication has been attempted for this message.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before marking this message as permanently failed.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the date and time after which the next retry attempt should be made, or null for immediate retry.
    /// Used to implement exponential backoff between retry attempts.
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracing this message across distributed systems.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last failed publication attempt, or null if no error occurred.
    /// </summary>
    public string? LastError { get; set; }
}
