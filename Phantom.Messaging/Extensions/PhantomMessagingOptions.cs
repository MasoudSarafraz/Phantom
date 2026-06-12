using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Messaging.RabbitMq;

namespace Phantom.Messaging.Extensions;

public class PhantomMessagingOptions
{
    public Dictionary<string, Action<ChannelBuilder>> ChannelBuilders { get; } = new();
    public Dictionary<Type, List<string>> EventChannelMappings { get; } = new();
    public bool UseOutbox { get; set; }
    public int OutboxBatchSize { get; set; } = 100;
    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public bool UseIdempotency { get; set; }

    public PhantomMessagingOptions AddChannel(string name, Action<ChannelBuilder> configure) { ChannelBuilders[name] = configure; return this; }
    public void RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent { EventChannelMappings[typeof(TEvent)] = channelNames.ToList(); }
    public void UseOutboxProcessing(int batchSize = 100, TimeSpan? pollingInterval = null) { UseOutbox = true; OutboxBatchSize = batchSize; OutboxPollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5); }
    public void EnableIdempotency() { UseIdempotency = true; }
    public void ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null) { }
    public void ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null) { }
}

public class ChannelBuilder
{
    public string Name { get; }
    public Type? AdapterType { get; private set; }
    public object? AdapterOptions { get; private set; }

    public ChannelBuilder(string name) { Name = name; }
    public ChannelBuilder UseInMemory() { AdapterType = typeof(InMemoryChannelAdapter); return this; }
    public ChannelBuilder UseRabbitMq(Action<RabbitMqOptions> configure) { var options = new RabbitMqOptions(); configure(options); AdapterType = typeof(RabbitMqChannelAdapter); AdapterOptions = options; return this; }
}
