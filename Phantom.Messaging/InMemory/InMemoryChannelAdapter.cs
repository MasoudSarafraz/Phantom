using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Messaging.Abstractions;
using System.Collections.Concurrent;

namespace Phantom.Messaging.InMemory;

public class InMemoryChannelAdapter : IChannelAdapter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryChannelAdapter> _logger;
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private volatile bool _isStarted;

    public string ChannelName { get; }

    /// <inheritdoc />
    public bool IsStarted => _isStarted;

    public InMemoryChannelAdapter(string channelName, IServiceProvider serviceProvider, ILogger<InMemoryChannelAdapter> logger)
    { ChannelName = channelName; _serviceProvider = serviceProvider; _logger = logger; }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        if (!_subscriptions.TryGetValue(typeof(TEvent), out var subscriptions)) return Task.CompletedTask;
        var tasks = subscriptions.Select(async sub =>
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService(sub.HandlerType);
            var handleMethod = sub.HandlerType.GetMethod("HandleAsync")!;
            await (Task)handleMethod.Invoke(handler, new object[] { @event, ct })!;
        });
        return Task.WhenAll(tasks);
    }

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>
    {
        var subscriptions = _subscriptions.GetOrAdd(typeof(TEvent), _ => new List<Subscription>());
        lock (subscriptions) { subscriptions.Add(new Subscription(typeof(THandler))); }
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _isStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _isStarted = false;
        return Task.CompletedTask;
    }

    private record Subscription(Type HandlerType);
}
