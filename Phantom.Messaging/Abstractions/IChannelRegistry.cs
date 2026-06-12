using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

public interface IChannelRegistry
{
    void Register(string channelName, IChannelAdapter adapter);

    IReadOnlyList<IChannelAdapter> GetChannels(string channelName);

    IReadOnlyList<IChannelAdapter> GetChannelsForEvent<TEvent>() where TEvent : IIntegrationEvent;

    void MapEventToChannel<TEvent>(string channelName) where TEvent : IIntegrationEvent;

    void MapEventToChannel(Type eventType, string channelName);

    void MapEventToChannels<TEvent>(params string[] channelNames) where TEvent : IIntegrationEvent;

    IReadOnlyCollection<string> GetAllChannelNames();

    IReadOnlyList<IChannelAdapter> GetAllAdapters();
}
