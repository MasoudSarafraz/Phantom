using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent;
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent;
}

public static class EventPublisherExtensions
{
    public static Task PublishAsync<TEvent>(
        this IEventPublisher publisher,
        TEvent @event,
        ChannelName channel,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent
        => publisher.PublishAsync(@event, channel.Value, ct);
}
