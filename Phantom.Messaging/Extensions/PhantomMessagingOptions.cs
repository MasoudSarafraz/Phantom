using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Messaging.RabbitMq;

namespace Phantom.Messaging.Extensions;

/// <summary>
/// Configuration options for the Phantom messaging system.
/// Provides a fluent API for setting up channels, routing, resilience, and outbox processing.
/// </summary>
public class PhantomMessagingOptions
{
    /// <summary>
    /// Gets the registered channel builders, keyed by channel name.
    /// </summary>
    public Dictionary<string, Action<ChannelBuilder>> ChannelBuilders { get; } = new();

    /// <summary>
    /// Gets the event-to-channel mappings, keyed by event type.
    /// </summary>
    public Dictionary<Type, List<string>> EventChannelMappings { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the transactional outbox pattern is enabled.
    /// </summary>
    public bool UseOutbox { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of outbox messages to process in a single batch.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval between outbox polling cycles.
    /// </summary>
    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets a value indicating whether idempotent message processing is enabled.
    /// When enabled, duplicate messages will be detected and safely ignored.
    /// </summary>
    public bool UseIdempotency { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to throw an exception when no channel adapter
    /// is found for a published event, instead of silently swallowing the event.
    /// </summary>
    public bool ThrowIfNoChannelFound { get; set; }

    /// <summary>
    /// Gets the retry configuration, or null if retry is not configured.
    /// </summary>
    public RetryOptions? Retry { get; private set; }

    /// <summary>
    /// Gets the circuit breaker configuration, or null if circuit breaker is not configured.
    /// </summary>
    public CircuitBreakerOptions? CircuitBreaker { get; private set; }

    /// <summary>
    /// Adds a named channel with the specified configuration.
    /// </summary>
    /// <param name="name">The logical name of the channel.</param>
    /// <param name="configure">An action to configure the channel builder.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public PhantomMessagingOptions AddChannel(string name, Action<ChannelBuilder> configure)
    {
        ChannelBuilders[name] = configure;
        return this;
    }

    /// <summary>
    /// Routes an integration event type to one or more named channels.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type to route.</typeparam>
    /// <param name="channelNames">The names of the channels to route the event to.</param>
    public void RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent
    {
        EventChannelMappings[typeof(TEvent)] = channelNames.ToList();
    }

    /// <summary>
    /// Enables the transactional outbox pattern for reliable event publishing.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to process per polling cycle.</param>
    /// <param name="pollingInterval">The interval between polling cycles, or null to use the default of 5 seconds.</param>
    public void UseOutboxProcessing(int batchSize = 100, TimeSpan? pollingInterval = null)
    {
        UseOutbox = true;
        OutboxBatchSize = batchSize;
        OutboxPollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Enables idempotent message processing to safely handle duplicate messages.
    /// </summary>
    public void EnableIdempotency()
    {
        UseIdempotency = true;
    }

    /// <summary>
    /// Configures retry resilience for message publishing.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts.</param>
    /// <param name="baseDelay">The base delay between retries (uses exponential backoff), or null for default of 1 second.</param>
    public void ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        Retry = new RetryOptions
        {
            MaxRetries = maxRetries,
            BaseDelay = baseDelay ?? TimeSpan.FromSeconds(1)
        };
    }

    /// <summary>
    /// Configures circuit breaker resilience for message publishing.
    /// </summary>
    /// <param name="failureThreshold">The number of consecutive failures before opening the circuit.</param>
    /// <param name="resetTimeout">The duration to keep the circuit open before attempting recovery, or null for default of 30 seconds.</param>
    public void ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        CircuitBreaker = new CircuitBreakerOptions
        {
            FailureThreshold = failureThreshold,
            ResetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30)
        };
    }
}

/// <summary>
/// Configuration options for retry resilience behavior.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retries. Exponential backoff is applied on top of this value.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Configuration options for circuit breaker resilience behavior.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the number of consecutive failures required to open the circuit breaker.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration to keep the circuit breaker open before transitioning to half-open state.
    /// </summary>
    public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Builder for configuring a messaging channel adapter.
/// </summary>
public class ChannelBuilder
{
    /// <summary>
    /// Gets the logical name of the channel being built.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the channel adapter, or null if not yet configured.
    /// </summary>
    public Type? AdapterType { get; private set; }

    /// <summary>
    /// Gets the adapter-specific options, or null if not yet configured.
    /// </summary>
    public object? AdapterOptions { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelBuilder"/> class.
    /// </summary>
    /// <param name="name">The logical name of the channel.</param>
    public ChannelBuilder(string name) { Name = name; }

    /// <summary>
    /// Configures this channel to use the in-memory transport.
    /// </summary>
    /// <returns>This builder instance for fluent chaining.</returns>
    public ChannelBuilder UseInMemory() { AdapterType = typeof(InMemoryChannelAdapter); return this; }

    /// <summary>
    /// Configures this channel to use the RabbitMQ transport.
    /// </summary>
    /// <param name="configure">An action to configure the RabbitMQ options.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public ChannelBuilder UseRabbitMq(Action<RabbitMqOptions> configure)
    {
        var options = new RabbitMqOptions();
        configure(options);
        AdapterType = typeof(RabbitMqChannelAdapter);
        AdapterOptions = options;
        return this;
    }
}
