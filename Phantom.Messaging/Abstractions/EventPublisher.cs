using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Default implementation of <see cref="IEventPublisher"/> that dispatches events
/// to channel adapters via the <see cref="IChannelRegistry"/>.
/// Uses parallel execution consistently across all overloads, with per-adapter error isolation.
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IChannelRegistry _registry;
    private readonly ILogger<EventPublisher> _logger;
    private readonly bool _throwIfNoChannelFound;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPublisher"/> class.
    /// </summary>
    /// <param name="registry">The channel registry for resolving channel adapters.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="throwIfNoChannelFound">
    /// If true, throws an <see cref="InvalidOperationException"/> when no channel adapter is found
    /// for the target channel or event type. If false (default), logs a warning and returns silently.
    /// </param>
    public EventPublisher(IChannelRegistry registry, ILogger<EventPublisher> logger, bool throwIfNoChannelFound = false)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _throwIfNoChannelFound = throwIfNoChannelFound;
    }

    /// <inheritdoc />
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

        // Parallel execution with per-adapter error isolation
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

    /// <inheritdoc />
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

        // Parallel execution with per-adapter error isolation (consistent with the channel-specific overload)
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
