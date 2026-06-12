using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using System.Collections.Concurrent;

namespace Phantom.Messaging.Abstractions;

public class ChannelRegistry : IChannelRegistry
{
    private readonly ConcurrentDictionary<string, List<IChannelAdapter>> _channels = new();
    private readonly ConcurrentDictionary<Type, List<string>> _eventChannelMap = new();
    private readonly ILogger<ChannelRegistry> _logger;

    public ChannelRegistry(ILogger<ChannelRegistry> logger) { _logger = logger; }

    public void Register(string channelName, IChannelAdapter adapter)
    {
        var adapters = _channels.GetOrAdd(channelName, _ => new List<IChannelAdapter>());
        lock (adapters) { adapters.Add(adapter); }
        _logger.LogInformation("[Phantom] Registered channel '{ChannelName}' with adapter '{AdapterType}'", channelName, adapter.GetType().Name);
    }

    public IReadOnlyList<IChannelAdapter> GetChannels(string channelName) =>
        _channels.TryGetValue(channelName, out var adapters) ? adapters.AsReadOnly() : Array.Empty<IChannelAdapter>();

    public IReadOnlyList<IChannelAdapter> GetChannelsForEvent<TEvent>() where TEvent : IIntegrationEvent
    {
        if (!_eventChannelMap.TryGetValue(typeof(TEvent), out var channelNames))
            return _channels.Values.SelectMany(c => c).ToList().AsReadOnly();
        return channelNames.SelectMany(name => GetChannels(name)).ToList().AsReadOnly();
    }

    public void MapEventToChannel<TEvent>(string channelName) where TEvent : IIntegrationEvent
    {
        var channels = _eventChannelMap.GetOrAdd(typeof(TEvent), _ => new List<string>());
        lock (channels) { if (!channels.Contains(channelName)) channels.Add(channelName); }
        _logger.LogInformation("[Phantom] Mapped event '{EventType}' to channel '{ChannelName}'", typeof(TEvent).Name, channelName);
    }

    public void MapEventToChannels<TEvent>(params string[] channelNames) where TEvent : IIntegrationEvent
    {
        foreach (var channelName in channelNames) MapEventToChannel<TEvent>(channelName);
    }
}
