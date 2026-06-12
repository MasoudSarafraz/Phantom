using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.Extensions;

/// <summary>
/// Hosted service that starts and stops all registered <see cref="IChannelAdapter"/> instances.
/// Ensures that message consumers (e.g., RabbitMQ consumers) are properly initialized on application startup.
/// </summary>
internal class ChannelAdapterHostedService : IHostedService
{
    private readonly IChannelRegistry _registry;
    private readonly ILogger<ChannelAdapterHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelAdapterHostedService"/> class.
    /// </summary>
    /// <param name="registry">The channel registry containing all registered adapters.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public ChannelAdapterHostedService(IChannelRegistry registry, ILogger<ChannelAdapterHostedService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
