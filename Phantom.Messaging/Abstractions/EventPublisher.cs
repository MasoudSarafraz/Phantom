using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

public class EventPublisher : IEventPublisher
{
    private readonly IChannelRegistry _registry;
    private readonly ILogger<EventPublisher> _logger;
    private readonly bool _throwIfNoChannelFound;

    public EventPublisher(IChannelRegistry registry, ILogger<EventPublisher> logger, bool throwIfNoChannelFound = false)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _throwIfNoChannelFound = throwIfNoChannelFound;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        if (ct.IsCancellationRequested) return;

        var adapters = _registry.GetChannels(channel);
        if (!adapters.Any())
        {
            if (_throwIfNoChannelFound)
                throw new InvalidOperationException($"No adapter found for channel '{channel}'.");
            _logger.LogWarning("[Phantom] No adapter found for channel '{Channel}'", channel);
            return;
        }

        var tasks = adapters.Select(async adapter =>
        {
            try
            {
                await adapter.PublishAsync(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Failed to publish {EventType} to channel '{Channel}' on adapter '{AdapterType}'",
                    typeof(TEvent).Name, channel, adapter.GetType().Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        if (ct.IsCancellationRequested) return;

        var adapters = _registry.GetChannelsForEvent<TEvent>();
        if (!adapters.Any())
        {
            if (_throwIfNoChannelFound)
                throw new InvalidOperationException($"No channels mapped for event '{typeof(TEvent).Name}'.");
            _logger.LogWarning("[Phantom] No channels mapped for event '{EventType}'", typeof(TEvent).Name);
            return;
        }

        var tasks = adapters.Select(async adapter =>
        {
            try
            {
                await adapter.PublishAsync(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Failed to publish {EventType} to adapter '{AdapterType}' on channel '{Channel}'",
                    typeof(TEvent).Name, adapter.GetType().Name, adapter.ChannelName);
            }
        });

        await Task.WhenAll(tasks);
    }
}
