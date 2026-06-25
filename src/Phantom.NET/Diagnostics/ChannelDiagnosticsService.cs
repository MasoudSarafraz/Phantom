using Phantom.Messaging.Abstractions;

namespace Phantom.NET.Diagnostics;

public class ChannelDiagnosticsService
{
    private readonly IChannelRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public ChannelDiagnosticsService(IChannelRegistry registry, IServiceProvider serviceProvider)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
    }

    public object GetChannelsSnapshot()
    {
        var channelNames = _registry.GetAllChannelNames();
        var result = new List<object>();

        foreach (var name in channelNames)
        {
            var adapters = _registry.GetChannels(name);
            foreach (var adapter in adapters)
            {
                result.Add(new
                {
                    channelName = name,
                    adapterType = adapter.GetType().Name,
                    isStarted = adapter.IsStarted,
                    channelAdapterName = adapter.ChannelName
                });
            }
        }

        return new
        {
            totalChannels = channelNames.Count,
            totalAdapters = result.Count,
            channels = result
        };
    }
}
