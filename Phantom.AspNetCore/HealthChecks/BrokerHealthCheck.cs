using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.Messaging.Abstractions;

namespace Phantom.AspNetCore.HealthChecks;

/// <summary>
/// Health check that verifies the messaging broker connectivity for a specific channel.
/// Uses <see cref="IServiceScopeFactory"/> to correctly resolve scoped dependencies
/// from the singleton health check lifetime. Checks adapter status via
/// <see cref="IChannelAdapter.IsStarted"/> instead of calling
/// <see cref="IChannelAdapter.StartAsync"/>, which would cause duplicate connections.
/// </summary>
public class BrokerHealthCheck : IHealthCheck
{
    private readonly string _channelName;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerHealthCheck"/> class.
    /// </summary>
    /// <param name="channelName">The name of the messaging channel to check.</param>
    /// <param name="scopeFactory">
    /// The service scope factory used to create a scope for resolving the scoped
    /// <see cref="IChannelRegistry"/>.
    /// </param>
    public BrokerHealthCheck(string channelName, IServiceScopeFactory scopeFactory)
    {
        _channelName = channelName;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Checks whether the messaging channel's adapters are started and connected.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="ct">A cancellation token to cancel the health check.</param>
    /// <returns>
    /// <see cref="HealthCheckResult.Healthy"/> if all adapters for the channel are started;
    /// <see cref="HealthCheckResult.Degraded"/> if some adapters are not yet started;
    /// <see cref="HealthCheckResult.Unhealthy"/> if no adapters are found or a timeout occurs.
    /// </returns>
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
