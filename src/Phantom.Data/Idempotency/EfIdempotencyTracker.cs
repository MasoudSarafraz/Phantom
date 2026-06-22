using Microsoft.EntityFrameworkCore;
using Phantom.Data.EfCore;
using Phantom.Infrastructure.Abstractions.Idempotency;

namespace Phantom.Data.Idempotency;

public class EfIdempotencyTracker : IIdempotencyTracker
{
    private readonly PhantomDbContext _dbContext;

    public EfIdempotencyTracker(PhantomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => await _dbContext.Set<ProcessedEvent>().AnyAsync(e => e.EventId == eventId, ct);

    public async Task MarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        var exists = await _dbContext.Set<ProcessedEvent>().AnyAsync(e => e.EventId == eventId, ct);
        if (exists) return;

        try
        {
            await _dbContext.Set<ProcessedEvent>().AddAsync(new ProcessedEvent
            {
                EventId = eventId,
                EventType = eventType
            }, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
        }
    }

    public async Task<bool> TryMarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        var exists = await _dbContext.Set<ProcessedEvent>().AnyAsync(e => e.EventId == eventId, ct);
        if (exists) return false;

        try
        {
            await _dbContext.Set<ProcessedEvent>().AddAsync(new ProcessedEvent
            {
                EventId = eventId,
                EventType = eventType
            }, ct);
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
