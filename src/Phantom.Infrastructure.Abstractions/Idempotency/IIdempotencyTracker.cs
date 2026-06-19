namespace Phantom.Infrastructure.Abstractions.Idempotency;

/// <summary>
/// Contract for a tracker that records which integration events have already been processed,
/// so that duplicate deliveries (which can happen under at-least-once broker semantics) do
/// not produce duplicate side effects.
///
/// Implementations live in the data layer (e.g., <c>EfIdempotencyTracker</c> in Phantom.Data).
/// </summary>
public interface IIdempotencyTracker
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    Task MarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
