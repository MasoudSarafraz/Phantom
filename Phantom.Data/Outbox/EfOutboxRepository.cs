using Microsoft.EntityFrameworkCore;
using Phantom.Data.EfCore;
using Phantom.Messaging.Outbox;

namespace Phantom.Data.Outbox;

public class EfOutboxRepository : IOutboxMessageRepository
{
    private readonly PhantomDbContext _dbContext;
    public EfOutboxRepository(PhantomDbContext dbContext) { _dbContext = dbContext; }

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default) => await _dbContext.Set<OutboxMessage>().AddAsync(message, ct);
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default) => await _dbContext.Set<OutboxMessage>().Where(m => !m.IsPublished).OrderBy(m => m.CreatedAt).Take(batchSize).ToListAsync(ct);
    public async Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default) { var m = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct); if (m != null) { m.IsPublished = true; m.PublishedAt = DateTime.UtcNow; _dbContext.Set<OutboxMessage>().Update(m); await _dbContext.SaveChangesAsync(ct); } }
    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default) { var m = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { messageId }, ct); if (m != null) { m.LastError = error; _dbContext.Set<OutboxMessage>().Update(m); await _dbContext.SaveChangesAsync(ct); } }
}
