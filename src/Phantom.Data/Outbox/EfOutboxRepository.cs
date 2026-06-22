using Microsoft.EntityFrameworkCore;
using Phantom.Data.EfCore;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.Outbox;

public class EfOutboxRepository : IOutboxMessageRepository
{
    private readonly PhantomDbContext _dbContext;

    public EfOutboxRepository(PhantomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => await _dbContext.Set<OutboxMessage>().AddAsync(message, ct);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => !m.IsPublished
                && m.RetryCount < m.MaxRetryCount
                && (m.NextRetryAt == null || m.NextRetryAt <= now)
                && (m.LastAttemptAt == null || m.LastAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.IsPublished = true;
            message.PublishedAt = DateTimeOffset.UtcNow;
            message.LastError = null;
            message.NextRetryAt = null;
        }
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.LastError = error;
            message.LastAttemptAt = DateTimeOffset.UtcNow;
        }
    }

    public async Task IncrementRetryCountAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message == null) return;

        message.RetryCount++;
        message.LastError = error;
        message.LastAttemptAt = DateTimeOffset.UtcNow;

        if (message.RetryCount >= message.MaxRetryCount)
        {
            message.NextRetryAt = null;
        }
        else
        {
            var backoffSeconds = Math.Min(
                Math.Pow(2, message.RetryCount) * 5,
                3600);
            message.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetFailedAsync(int maxRetryCount, int batchSize, CancellationToken ct = default)
        => await _dbContext.Set<OutboxMessage>()
            .Where(m => !m.IsPublished && m.LastError != null && m.RetryCount < maxRetryCount)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<bool> TryMarkAsProcessingAsync(Guid messageId, DateTimeOffset lockedUntil, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message == null) return false;
        if (message.IsPublished) return false;

        var now = DateTimeOffset.UtcNow;
        if (message.LastAttemptAt is not null && message.LastAttemptAt > now)
        {
            return false;
        }

        message.LastAttemptAt = lockedUntil;
        return true;
    }

    public async Task ClearProcessingLockAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null && !message.IsPublished)
        {
            message.LastAttemptAt = null;
        }
    }

    public async Task MarkAsTerminalFailureAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.LastError = error;
            message.LastAttemptAt = DateTimeOffset.UtcNow;
            message.RetryCount = message.MaxRetryCount;
            message.NextRetryAt = null;
        }
    }
}
