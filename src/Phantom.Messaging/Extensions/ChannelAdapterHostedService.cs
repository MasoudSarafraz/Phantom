using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.Extensions;

internal class ChannelAdapterHostedService : IHostedService
{
    private readonly IChannelRegistry _registry;
    private readonly ILogger<ChannelAdapterHostedService> _logger;

    public ChannelAdapterHostedService(IChannelRegistry registry, ILogger<ChannelAdapterHostedService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_registry is not ChannelRegistry concreteRegistry) return;

        var adapters = concreteRegistry.GetAllAdapters();
        _logger.LogInformation("[Phantom] Starting {AdapterCount} channel adapter(s)...", adapters.Count);

        foreach (var adapter in adapters)
        {
            try
            {
                await adapter.StartAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Failed to start channel adapter '{ChannelName}' of type '{AdapterType}'",
                    adapter.ChannelName, adapter.GetType().Name);
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_registry is not ChannelRegistry concreteRegistry) return;

        var adapters = concreteRegistry.GetAllAdapters();
        _logger.LogInformation("[Phantom] Stopping {AdapterCount} channel adapter(s)...", adapters.Count);

        foreach (var adapter in adapters)
        {
            try
            {
                await adapter.StopAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Failed to stop channel adapter '{ChannelName}' of type '{AdapterType}'",
                    adapter.ChannelName, adapter.GetType().Name);
            }
        }
    }
}
