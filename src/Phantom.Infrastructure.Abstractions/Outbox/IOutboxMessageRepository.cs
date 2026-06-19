namespace Phantom.Infrastructure.Abstractions.Outbox;

/// <summary>
/// Contract for a repository that persists and queries <see cref="OutboxMessage"/> rows.
/// Implementations live in the data layer (e.g., <c>EfOutboxRepository</c> in Phantom.Data).
/// </summary>
public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetFailedAsync(int maxRetryCount, int batchSize, CancellationToken ct = default);

    Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default);

    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default);

    Task IncrementRetryCountAsync(Guid messageId, string error, CancellationToken ct = default);
}
