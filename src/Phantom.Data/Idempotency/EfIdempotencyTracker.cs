using Microsoft.EntityFrameworkCore;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Data.EfCore;

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
        => await _dbContext.Set<ProcessedEvent>().AddAsync(new ProcessedEvent
        {
            EventId = eventId,
            EventType = eventType
        }, ct);
}
