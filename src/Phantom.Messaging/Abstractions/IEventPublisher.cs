using Phantom.Core.Events;

namespace Phantom.Messaging.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent;
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent;
}

/// <summary>
/// Extension methods that expose the strongly-typed <see cref="ChannelName"/> overloads
/// for <see cref="IEventPublisher"/>. They delegate to the string-based API so that
/// existing implementations do not need to change.
/// </summary>
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
