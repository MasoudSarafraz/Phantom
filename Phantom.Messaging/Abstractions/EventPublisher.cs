using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

public class EventPublisher : IEventPublisher
{
    private readonly IChannelRegistry _registry;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IChannelRegistry registry, ILogger<EventPublisher> logger) { _registry = registry; _logger = logger; }

    public async Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        var adapters = _registry.GetChannels(channel);
        if (!adapters.Any()) { _logger.LogWarning("[Phantom] No adapter found for channel '{Channel}'", channel); return; }
        foreach (var adapter in adapters) await adapter.PublishAsync(@event, ct);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        var adapters = _registry.GetChannelsForEvent<TEvent>();
        if (!adapters.Any()) { _logger.LogWarning("[Phantom] No channels mapped for event '{EventType}'", typeof(TEvent).Name); return; }
        await Task.WhenAll(adapters.Select(adapter => adapter.PublishAsync(@event, ct)));
    }
}
