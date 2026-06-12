using Microsoft.EntityFrameworkCore;
using Phantom.Data.EfCore;
using Phantom.Messaging.Outbox;

namespace Phantom.Data.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutboxMessageRepository"/>.
/// Does NOT call SaveChangesAsync from Mark* methods — the caller (UnitOfWork or OutboxProcessor)
/// is responsible for committing changes to ensure transactional consistency.
/// </summary>
public class EfOutboxRepository : IOutboxMessageRepository
{
    private readonly PhantomDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfOutboxRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public EfOutboxRepository(PhantomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => await _dbContext.Set<OutboxMessage>().AddAsync(message, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default)
        => await _dbContext.Set<OutboxMessage>()
            .Where(m => !m.IsPublished)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    /// <inheritdoc/>
    /// <remarks>Only tracks the change; the caller must call SaveChanges to commit.</remarks>
    public async Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.IsPublished = true;
            message.PublishedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Only tracks the change; the caller must call SaveChanges to commit.</remarks>
    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.LastError = error;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Only tracks the change; the caller must call SaveChanges to commit.</remarks>
    public async Task IncrementRetryCountAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct);
        if (message != null)
        {
            message.RetryCount++;
            message.LastError = error;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OutboxMessage>> GetFailedAsync(int maxRetryCount, int batchSize, CancellationToken ct = default)
        => await _dbContext.Set<OutboxMessage>()
            .Where(m => !m.IsPublished && m.LastError != null && m.RetryCount < maxRetryCount)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
}
