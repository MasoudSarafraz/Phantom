using Phantom.Core.Events;
using Phantom.Core.Messaging;

namespace Phantom.Messaging.Abstractions;

public interface IChannelAdapter
{
    string ChannelName { get; }
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent;
    void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
