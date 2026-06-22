namespace Phantom.Infrastructure.Abstractions.Outbox;

public class OutboxMessage
{

    public const string DefaultChannel = "default";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventType { get; set; } = default!;

    public string Payload { get; set; } = default!;

    public string Channel { get; set; } = DefaultChannel;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsPublished { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 5;

    public DateTimeOffset? NextRetryAt { get; set; }

    public string? CorrelationId { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public bool IsTerminalFailure => !IsPublished && RetryCount >= MaxRetryCount;
}
