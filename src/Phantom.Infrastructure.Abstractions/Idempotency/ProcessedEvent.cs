namespace Phantom.Infrastructure.Abstractions.Idempotency;

/// <summary>
/// Persistent record of an integration event that has already been processed.
/// Used by <c>IIdempotencyTracker</c> implementations to guarantee at-most-once processing.
///
/// This is an infrastructure concern, not a domain concept — hence it lives in
/// <c>Phantom.Infrastructure.Abstractions</c> rather than <c>Phantom.Core</c>.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = default!;

    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
