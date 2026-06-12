using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Defines a registry for managing channel adapters and event-to-channel routing mappings.
/// </summary>
public interface IChannelRegistry
{
    /// <summary>
    /// Registers a channel adapter under the specified channel name.
    /// </summary>
    /// <param name="channelName">The logical name of the channel.</param>
    /// <param name="adapter">The channel adapter to register.</param>
    void Register(string channelName, IChannelAdapter adapter);

    /// <summary>
    /// Gets all channel adapters registered under the specified channel name.
    /// </summary>
    /// <param name="channelName">The logical name of the channel.</param>
    /// <returns>A read-only list of channel adapters, or an empty list if no adapters are registered.</returns>
    IReadOnlyList<IChannelAdapter> GetChannels(string channelName);

    /// <summary>
    /// Gets all channel adapters mapped for the specified integration event type.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    /// <returns>A read-only list of channel adapters mapped for the event type, or an empty list if no mapping exists.</returns>
    IReadOnlyList<IChannelAdapter> GetChannelsForEvent<TEvent>() where TEvent : IIntegrationEvent;

    /// <summary>
    /// Maps an integration event type to a named channel using a generic type parameter.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type to map.</typeparam>
    /// <param name="channelName">The name of the channel to map the event to.</param>
    void MapEventToChannel<TEvent>(string channelName) where TEvent : IIntegrationEvent;

    /// <summary>
    /// Maps an integration event type to a named channel using a runtime type.
    /// This overload avoids the need for reflection when the event type is not known at compile time.
    /// </summary>
    /// <param name="eventType">The runtime type of the integration event to map.</param>
    /// <param name="channelName">The name of the channel to map the event to.</param>
    void MapEventToChannel(Type eventType, string channelName);

    /// <summary>
    /// Maps an integration event type to multiple named channels.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type to map.</typeparam>
    /// <param name="channelNames">The names of the channels to map the event to.</param>
    void MapEventToChannels<TEvent>(params string[] channelNames) where TEvent : IIntegrationEvent;

    /// <summary>
    /// Gets the names of all registered channels.
    /// </summary>
    /// <returns>A read-only collection of channel names.</returns>
    IReadOnlyCollection<string> GetAllChannelNames();

    /// <summary>
    /// Gets all registered channel adapters across all channels.
    /// </summary>
    /// <returns>A read-only list of all registered channel adapters.</returns>
    IReadOnlyList<IChannelAdapter> GetAllAdapters();
}
