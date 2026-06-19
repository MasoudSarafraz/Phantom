using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Publishes integration events to one or more <see cref="IChannelAdapter"/> instances
/// discovered through the <see cref="IChannelRegistry"/>. Publishing is wrapped in the
/// configured <see cref="IResiliencePipeline"/> so that transient broker failures are
/// retried and protected by a circuit breaker.
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IChannelRegistry _registry;
    private readonly IResiliencePipeline _resilience;
    private readonly ILogger<EventPublisher> _logger;
    private readonly bool _throwIfNoChannelFound;

    public EventPublisher(
        IChannelRegistry registry,
        IResiliencePipeline resilience,
        ILogger<EventPublisher> logger,
        bool throwIfNoChannelFound = false)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
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

        // Wrap the per-adapter publish with the resilience pipeline so that retry/circuit-breaker
        // applies to the broker interaction (not to the per-adapter parallel fan-out).
        var tasks = adapters.Select(adapter => PublishWithResilienceAsync(adapter, @event, channel, ct));
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

        var tasks = adapters.Select(adapter => PublishWithResilienceAsync(adapter, @event, adapter.ChannelName, ct));
        await Task.WhenAll(tasks);
    }

    private async Task PublishWithResilienceAsync<TEvent>(
        IChannelAdapter adapter,
        TEvent @event,
        string channel,
        CancellationToken ct) where TEvent : IIntegrationEvent
    {
        try
        {
            await _resilience.ExecuteAsync(
                async token => await adapter.PublishAsync(@event, token),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Phantom] Failed to publish {EventType} to adapter '{AdapterType}' on channel '{Channel}' after resilience pipeline",
                typeof(TEvent).Name, adapter.GetType().Name, channel);
            throw;
        }
    }
}
