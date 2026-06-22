namespace Phantom.Infrastructure.Abstractions.Idempotency;

public interface IIdempotencyTracker
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    Task MarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);

    Task<bool> TryMarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
