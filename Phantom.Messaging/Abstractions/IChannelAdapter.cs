using Phantom.Core.Events;
using Phantom.Core.Messaging;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Represents a channel adapter that can publish and subscribe to integration events
/// over a specific messaging transport.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// The name of the channel this adapter is bound to.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Gets whether this adapter has been started and is currently connected.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Publishes an integration event to this channel.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    /// <param name="ct">A cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent;

    /// <summary>
    /// Subscribes a handler for a specific event type on this channel.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <typeparam name="THandler">The handler type that processes the event.</typeparam>
    void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>;

    /// <summary>
    /// Starts the channel adapter, establishing connections and consumers.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops the channel adapter, closing connections and releasing resources.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    Task StopAsync(CancellationToken ct = default);
}
