using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.Messaging.Abstractions;

namespace Phantom.AspNetCore.HealthChecks;

public class BrokerHealthCheck : IHealthCheck
{
    private readonly string _channelName;
    private readonly IChannelRegistry _registry;

    public BrokerHealthCheck(string channelName, IChannelRegistry registry) { _channelName = channelName; _registry = registry; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var channels = _registry.GetChannels(_channelName);
            if (!channels.Any()) return HealthCheckResult.Unhealthy($"Channel '{_channelName}' not found");
            foreach (var adapter in channels) await adapter.StartAsync(ct);
            return HealthCheckResult.Healthy($"Channel '{_channelName}' is connected");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy($"Channel '{_channelName}' is unavailable: {ex.Message}", ex); }
    }
}
