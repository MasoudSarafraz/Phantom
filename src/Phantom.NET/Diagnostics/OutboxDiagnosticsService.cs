using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Data.EfCore;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.NET.Diagnostics;

public class OutboxDiagnosticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDiagnosticsService> _logger;

    public OutboxDiagnosticsService(IServiceScopeFactory scopeFactory, ILogger<OutboxDiagnosticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<object> GetOutboxSnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<PhantomDbContext>();

            if (dbContext is null)
            {
                return new { enabled = false, reason = "PhantomDbContext is not registered." };
            }

            var outboxSet = dbContext.Set<OutboxMessage>();
            var pendingCount = await outboxSet.LongCountAsync(m => !m.IsPublished && m.RetryCount < m.MaxRetryCount, ct);
            var terminalFailureCount = await outboxSet.LongCountAsync(m => !m.IsPublished && m.RetryCount >= m.MaxRetryCount, ct);
            var publishedCount = await outboxSet.LongCountAsync(m => m.IsPublished, ct);
            var total = await outboxSet.LongCountAsync(ct);

            var now = DateTimeOffset.UtcNow;
            var inBackoffCount = await outboxSet.LongCountAsync(
                m => !m.IsPublished && m.NextRetryAt != null && m.NextRetryAt > now, ct);

            var oldestPending = await outboxSet
                .Where(m => !m.IsPublished)
                .OrderBy(m => m.CreatedAt)
                .Select(m => (DateTimeOffset?)m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var recentErrors = await outboxSet
                .Where(m => !m.IsPublished && m.LastError != null)
                .OrderByDescending(m => m.LastAttemptAt)
                .Take(10)
                .Select(m => new
                {
                    messageId = m.Id,
                    eventType = m.EventType,
                    retryCount = m.RetryCount,
                    maxRetryCount = m.MaxRetryCount,
                    lastError = m.LastError,
                    lastAttemptAt = m.LastAttemptAt,
                    nextRetryAt = m.NextRetryAt,
                    channel = m.Channel
                })
                .ToListAsync(ct);

            return new
            {
                enabled = true,
                summary = new
                {
                    total,
                    pending = pendingCount,
                    inBackoff = inBackoffCount,
                    terminalFailures = terminalFailureCount,
                    published = publishedCount
                },
                oldestPendingAt = oldestPending,
                recentErrors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to gather outbox diagnostics.");
            return new { enabled = false, error = ex.Message };
        }
    }
}

public class IdempotencyDiagnosticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyDiagnosticsService> _logger;

    public IdempotencyDiagnosticsService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyDiagnosticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<object> GetIdempotencySnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();

            if (tracker is null)
            {
                return new { enabled = false };
            }

            using var dbScope = _scopeFactory.CreateScope();
            var dbContext = dbScope.ServiceProvider.GetService<PhantomDbContext>();

            if (dbContext is null)
            {
                return new { enabled = true, note = "Tracker is registered but no PhantomDbContext available for stats." };
            }

            var processedSet = dbContext.Set<ProcessedEvent>();
            var totalProcessed = await processedSet.LongCountAsync(ct);
            var processedInLast24h = await processedSet.LongCountAsync(e => e.ProcessedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);
            var processedInLast1h = await processedSet.LongCountAsync(e => e.ProcessedAt >= DateTimeOffset.UtcNow.AddHours(-1), ct);

            var topEventTypes = await processedSet
                .GroupBy(e => e.EventType)
                .Select(g => new { eventType = g.Key, count = g.LongCount() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync(ct);

            return new
            {
                enabled = true,
                summary = new
                {
                    totalProcessed,
                    processedInLast24Hours = processedInLast24h,
                    processedInLast1Hour = processedInLast1h
                },
                topEventTypes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to gather idempotency diagnostics.");
            return new { enabled = false, error = ex.Message };
        }
    }
}
