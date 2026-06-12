using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.Data.EfCore;

namespace Phantom.AspNetCore.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Phantom database.
/// Uses <see cref="IServiceScopeFactory"/> to correctly resolve the scoped
/// <see cref="PhantomDbContext"/> from the singleton health check lifetime.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="scopeFactory">
    /// The service scope factory used to create a scope for resolving the scoped
    /// <see cref="PhantomDbContext"/>.
    /// </param>
    public DatabaseHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Checks whether the database is accessible by attempting to open a connection.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="ct">A cancellation token to cancel the health check.</param>
    /// <returns>
    /// <see cref="HealthCheckResult.Healthy"/> if the database is reachable;
    /// <see cref="HealthCheckResult.Unhealthy"/> otherwise.
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhantomDbContext>();

            var canConnect = await dbContext.Database.CanConnectAsync(ct);
            return canConnect
                ? HealthCheckResult.Healthy("Database is accessible")
                : HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database is unavailable: {ex.Message}", ex);
        }
    }
}
