using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Kafka;
using Phantom.Messaging.RabbitMq;

namespace Phantom.Messaging.Extensions;

public class PhantomMessagingOptions
{
    public Dictionary<string, Action<ChannelBuilder>> ChannelBuilders { get; } = new();

    public Dictionary<Type, List<string>> EventChannelMappings { get; } = new();

    public bool UseOutbox { get; set; } = true;

    public int OutboxBatchSize { get; set; } = 100;

    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public bool UseIdempotency { get; set; }

    public bool ThrowIfNoChannelFound { get; set; }

    public RetryOptions? Retry { get; private set; }

    public CircuitBreakerOptions? CircuitBreaker { get; private set; }

    public PhantomMessagingOptions AddChannel(string name, Action<ChannelBuilder> configure)
    {
        ChannelBuilders[name] = configure;
        return this;
    }

    public void RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent
    {
        EventChannelMappings[typeof(TEvent)] = channelNames.ToList();
    }

    public void UseOutboxProcessing(int batchSize = 100, TimeSpan? pollingInterval = null)
    {
        UseOutbox = true;
        OutboxBatchSize = batchSize;
        OutboxPollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    public void EnableIdempotency()
    {
        UseIdempotency = true;
    }

    public void ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        Retry = new RetryOptions
        {
            MaxRetries = maxRetries,
            BaseDelay = baseDelay ?? TimeSpan.FromSeconds(1)
        };
    }

    public void ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = failureThreshold,
            ResetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30)
        };
    }
}

public class RetryOptions
{
    public int MaxRetries { get; set; } = 3;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;

    public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class ChannelBuilder
{
    public string Name { get; }

    public Type? AdapterType { get; private set; }

    public object? AdapterOptions { get; private set; }

    public ChannelBuilder(string name) { Name = name; }

    public ChannelBuilder UseInMemory() { AdapterType = typeof(InMemoryChannelAdapter); return this; }

    public ChannelBuilder UseRabbitMq(Action<RabbitMqOptions> configure)
    {
        var options = new RabbitMqOptions();
        configure(options);
        AdapterType = typeof(RabbitMqChannelAdapter);
        AdapterOptions = options;
        return this;
    }

    public ChannelBuilder UseKafka(Action<KafkaOptions> configure)
    {
        var options = new KafkaOptions();
        configure(options);
        AdapterType = typeof(KafkaChannelAdapter);
        AdapterOptions = options;
        return this;
    }
}
