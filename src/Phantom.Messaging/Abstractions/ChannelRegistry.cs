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
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name must not be empty or whitespace.", nameof(channelName));

        if (adapter is null)
            throw new ArgumentNullException(nameof(adapter));

        var adapters = _channels.GetOrAdd(channelName, _ => new List<IChannelAdapter>());
        lock (adapters)
        {
            if (adapters.Contains(adapter))
            {
                _logger.LogWarning("[Phantom] Adapter '{AdapterType}' already registered for channel '{ChannelName}'. Skipping duplicate.",
                    adapter.GetType().Name, channelName);
                return;
            }
            adapters.Add(adapter);
        }
        _logger.LogInformation("[Phantom] Registered channel '{ChannelName}' with adapter '{AdapterType}'", channelName, adapter.GetType().Name);
    }

    public IReadOnlyList<IChannelAdapter> GetChannels(string channelName)
    {
        if (!_channels.TryGetValue(channelName, out var adapters)) return Array.Empty<IChannelAdapter>();
        lock (adapters) { return adapters.ToList().AsReadOnly(); }
    }

    public IReadOnlyList<IChannelAdapter> GetChannelsForEvent<TEvent>() where TEvent : IIntegrationEvent
    {
        if (!_eventChannelMap.TryGetValue(typeof(TEvent), out var channelNames))
        {
            _logger.LogWarning("[Phantom] No channel mapping found for event '{EventType}'; returning empty list", typeof(TEvent).Name);
            return Array.Empty<IChannelAdapter>();
        }

        List<string> channelNamesCopy;
        lock (channelNames) { channelNamesCopy = channelNames.ToList(); }

        var result = new List<IChannelAdapter>();
        var seen = new HashSet<IChannelAdapter>(AdapterReferenceComparer.Instance);

        foreach (var name in channelNamesCopy)
        {
            if (!_channels.TryGetValue(name, out var adapters)) continue;
            List<IChannelAdapter> adaptersCopy;
            lock (adapters) { adaptersCopy = adapters.ToList(); }

            foreach (var adapter in adaptersCopy)
            {
                if (seen.Add(adapter))
                {
                    result.Add(adapter);
                }
            }
        }

        return result.AsReadOnly();
    }

    public void MapEventToChannel<TEvent>(string channelName) where TEvent : IIntegrationEvent
    {
        MapEventToChannel(typeof(TEvent), channelName);
    }

    public void MapEventToChannel(Type eventType, string channelName)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));

        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name must not be empty or whitespace.", nameof(channelName));

        var channels = _eventChannelMap.GetOrAdd(eventType, _ => new List<string>());
        lock (channels) { if (!channels.Contains(channelName)) channels.Add(channelName); }
        _logger.LogInformation("[Phantom] Mapped event '{EventType}' to channel '{ChannelName}'", eventType.Name, channelName);
    }

    public void MapEventToChannels<TEvent>(params string[] channelNames) where TEvent : IIntegrationEvent
    {
        foreach (var channelName in channelNames) MapEventToChannel<TEvent>(channelName);
    }

    public IReadOnlyCollection<string> GetAllChannelNames() =>
        _channels.Keys.ToList().AsReadOnly();

    public IReadOnlyList<IChannelAdapter> GetAllAdapters()
    {
        var seen = new HashSet<IChannelAdapter>(AdapterReferenceComparer.Instance);
        foreach (var list in _channels.Values)
        {
            lock (list)
            {
                foreach (var adapter in list)
                {
                    seen.Add(adapter);
                }
            }
        }
        return seen.ToList().AsReadOnly();
    }
}

internal sealed class AdapterReferenceComparer : IEqualityComparer<IChannelAdapter>
{
    public static readonly AdapterReferenceComparer Instance = new();

    public bool Equals(IChannelAdapter? x, IChannelAdapter? y) => ReferenceEquals(x, y);

    public int GetHashCode(IChannelAdapter obj) =>
        obj is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
