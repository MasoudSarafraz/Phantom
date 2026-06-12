using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.Data.EfCore;

namespace Phantom.AspNetCore.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly PhantomDbContext _dbContext;
    public DatabaseHealthCheck(PhantomDbContext dbContext) { _dbContext = dbContext; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(ct);
            return canConnect ? HealthCheckResult.Healthy("Database is accessible") : HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy($"Database is unavailable: {ex.Message}", ex); }
    }
}
