using Phantom.Data.Extensions;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.Kafka;
using Phantom.Messaging.RabbitMq;

namespace Phantom.NET.Extensions;

public class PhantomOptions
{
    internal PhantomDataOptions DataOptions { get; } = new();
    internal PhantomMessagingOptions MessagingOptions { get; } = new();
    internal bool UseCQRS { get; set; } = true;
    internal bool UseValidation { get; set; }

    public PhantomOptions UsePostgreSQL(string connectionString, Action<PhantomDataOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

        DataOptions.Provider = DatabaseProvider.PostgreSQL;
        DataOptions.ConnectionString = connectionString;
        configure?.Invoke(DataOptions);
        return this;
    }

    public PhantomOptions UseSqlServer(string connectionString, Action<PhantomDataOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

        DataOptions.Provider = DatabaseProvider.SqlServer;
        DataOptions.ConnectionString = connectionString;
        configure?.Invoke(DataOptions);
        return this;
    }

    public PhantomOptions UseInMemoryDatabase(Action<PhantomDataOptions>? configure = null)
    {
        DataOptions.Provider = DatabaseProvider.InMemory;
        configure?.Invoke(DataOptions);
        return this;
    }

    public PhantomOptions UseRabbitMq(string host, Action<RabbitMqOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("RabbitMQ host must not be null or empty.", nameof(host));

        MessagingOptions.AddChannel("default", c => c.UseRabbitMq(r =>
        {
            r.Host = host;
            configure?.Invoke(r);
        }));
        return this;
    }

    /// <summary>
    /// Configures a default Kafka channel with the specified bootstrap servers.
    /// This is a convenience method that creates a channel named "default" using Kafka.
    /// For multi-channel setups, use <see cref="AddChannel"/> with <c>channel.UseKafka(...)</c> instead.
    /// </summary>
    public PhantomOptions UseKafka(string bootstrapServers, Action<KafkaOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
            throw new ArgumentException("Kafka bootstrap servers must not be null or empty.", nameof(bootstrapServers));

        MessagingOptions.AddChannel("default", c => c.UseKafka(k =>
        {
            k.BootstrapServers = bootstrapServers;
            configure?.Invoke(k);
        }));
        return this;
    }

    public PhantomOptions AddChannel(string name, Action<ChannelBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name must not be null or empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(configure);

        MessagingOptions.AddChannel(name, configure);
        return this;
    }

    /// <summary>
    /// Strongly-typed overload of <see cref="AddChannel(string, Action{ChannelBuilder})"/>.
    /// Use this with a <c>ChannelName</c> declared as a constant in your application so
    /// typos at registration or publish time become compile-time errors.
    /// </summary>
    public PhantomOptions AddChannel(ChannelName name, Action<ChannelBuilder> configure)
        => AddChannel(name.Value, configure);

    public PhantomOptions UseFluentValidation()
    {
        UseValidation = true;
        return this;
    }

    public PhantomOptions UseSoftDelete()
    {
        DataOptions.UseSoftDelete = true;
        return this;
    }

    public PhantomOptions UseAuditable()
    {
        DataOptions.UseAuditable = true;
        return this;
    }

    public PhantomOptions UseOutbox()
    {
        DataOptions.UseOutbox = true;
        MessagingOptions.UseOutboxProcessing();
        return this;
    }

    /// <summary>
    /// Disables the outbox pattern. Not recommended for production — domain events may be lost on failure.
    /// Use only for simple CRUD scenarios or testing where event reliability is not required.
    /// </summary>
    public PhantomOptions DisableOutbox()
    {
        DataOptions.UseOutbox = false;
        MessagingOptions.UseOutbox = false;
        return this;
    }

    public PhantomOptions EnableIdempotency()
    {
        DataOptions.UseIdempotency = true;
        MessagingOptions.EnableIdempotency();
        return this;
    }

    public PhantomOptions RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent
    {
        if (channelNames is null || channelNames.Length == 0)
            throw new ArgumentException("At least one channel name must be specified.", nameof(channelNames));

        MessagingOptions.RouteEvent<TEvent>(channelNames);
        return this;
    }

    /// <summary>
    /// Strongly-typed overload of <see cref="RouteEvent{TEvent}(string[])"/> that accepts
    /// <see cref="ChannelName"/> instances instead of raw strings.
    /// </summary>
    public PhantomOptions RouteEvent<TEvent>(params ChannelName[] channelNames) where TEvent : Core.Events.IIntegrationEvent
    {
        if (channelNames is null || channelNames.Length == 0)
            throw new ArgumentException("At least one channel name must be specified.", nameof(channelNames));

        var names = channelNames.Select(c => c.Value).ToArray();
        MessagingOptions.RouteEvent<TEvent>(names);
        return this;
    }

    public PhantomOptions ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        if (maxRetries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be positive.");

        MessagingOptions.ConfigureRetry(maxRetries, baseDelay);
        return this;
    }

    public PhantomOptions ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        if (failureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be positive.");

        MessagingOptions.ConfigureCircuitBreaker(failureThreshold, resetTimeout);
        return this;
    }

    /// <summary>
    /// Validates the configuration invariants that, if violated, would otherwise only surface
    /// at runtime as cryptic resolution errors. This is called by <c>AddPhantom</c> at startup
    /// so that misconfiguration fails the application boot instead of failing later.
    ///
    /// The checks are intentionally conservative: they only flag combinations of options that
    /// cannot possibly work. Anything ambiguous is left for the runtime to decide.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the combination of configured options is impossible to satisfy.
    /// The exception message describes exactly which combination is broken.
    /// </exception>
    internal void Validate()
    {
        // 1) Database provider must be selected.
        if (DataOptions.Provider == DatabaseProvider.InMemory)
        {
            // InMemory is the default; allowed for dev/test but should warn if Outbox/Idempotency is on.
            if (DataOptions.UseOutbox || DataOptions.UseIdempotency)
            {
                // We do NOT throw — InMemory + Outbox works for tests — but the user should know.
                // Logged by the framework at startup; nothing to enforce here.
            }
        }
        else if (string.IsNullOrWhiteSpace(DataOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Database provider '{DataOptions.Provider}' is configured but no connection string was provided. " +
                "Call UsePostgreSQL(connectionString) or UseSqlServer(connectionString) with a valid connection string.");
        }

        // 2) If any messaging feature is configured, at least one channel must exist.
        //    A user who calls RouteEvent but forgot AddChannel gets a silent no-op at publish time otherwise.
        if (MessagingOptions.EventChannelMappings.Count > 0 && MessagingOptions.ChannelBuilders.Count == 0)
        {
            throw new InvalidOperationException(
                "Integration events are routed via RouteEvent(...) but no channel has been registered. " +
                "Call AddChannel(name, c => c.UseRabbitMq(...)/UseKafka(...)/UseInMemory()) at least once.");
        }

        // 3) Every RouteEvent target channel must exist.
        var registeredChannels = MessagingOptions.ChannelBuilders.Keys.ToHashSet();
        foreach (var (eventType, channels) in MessagingOptions.EventChannelMappings)
        {
            foreach (var channel in channels)
            {
                if (!registeredChannels.Contains(channel))
                {
                    throw new InvalidOperationException(
                        $"Integration event '{eventType.Name}' is routed to channel '{channel}' " +
                        "but no channel with that name has been registered via AddChannel(...). " +
                        "Either register the channel or fix the routing.");
                }
            }
        }

        // 4) If Outbox is enabled, the data layer needs a connection. (For InMemory provider,
        //    it's allowed because the EF InMemory provider works without a connection string.)
        if (DataOptions.UseOutbox
            && DataOptions.Provider != DatabaseProvider.InMemory
            && string.IsNullOrWhiteSpace(DataOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                "Outbox is enabled but no database connection is configured. " +
                "The outbox table must live in a real database; call UsePostgreSQL/UseSqlServer.");
        }

        // 5) Circuit breaker without retry is technically allowed but almost never what you want.
        //    We do NOT throw — but we surface the implication in a single visible place.
        // (Intentionally not enforced — kept here for documentation of the convention.)
    }
}
