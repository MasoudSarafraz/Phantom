namespace Phantom.Infrastructure.Abstractions.Idempotency;

public class ProcessedEvent
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = default!;

    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
