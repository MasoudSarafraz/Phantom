namespace Phantom.Infrastructure.Abstractions.Outbox;

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetFailedAsync(int maxRetryCount, int batchSize, CancellationToken ct = default);

    Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default);

    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default);

    Task IncrementRetryCountAsync(Guid messageId, string error, CancellationToken ct = default);

    Task<bool> TryMarkAsProcessingAsync(Guid messageId, DateTimeOffset lockedUntil, CancellationToken ct = default);

    Task ClearProcessingLockAsync(Guid messageId, CancellationToken ct = default);

    Task MarkAsTerminalFailureAsync(Guid messageId, string error, CancellationToken ct = default);
}
