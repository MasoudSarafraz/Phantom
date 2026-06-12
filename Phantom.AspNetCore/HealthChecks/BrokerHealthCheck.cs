using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.Messaging.Abstractions;

namespace Phantom.AspNetCore.HealthChecks;

public class BrokerHealthCheck : IHealthCheck
{
    private readonly string _channelName;
    private readonly IServiceScopeFactory _scopeFactory;

    public BrokerHealthCheck(string channelName, IServiceScopeFactory scopeFactory)
    {
        _channelName = channelName;
        _scopeFactory = scopeFactory;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IChannelRegistry>();

            var channels = registry.GetChannels(_channelName);
            if (!channels.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Channel '{_channelName}' not found"));
            }

            var allStarted = channels.All(adapter => adapter.IsStarted);

            if (allStarted)
            {
                return Task.FromResult(HealthCheckResult.Healthy($"Channel '{_channelName}' is connected"));
            }

            return Task.FromResult(HealthCheckResult.Degraded($"Channel '{_channelName}' has adapters that are not yet started"));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Health check for channel '{_channelName}' was canceled"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Channel '{_channelName}' is unavailable: {ex.Message}", ex));
        }
    }
}
