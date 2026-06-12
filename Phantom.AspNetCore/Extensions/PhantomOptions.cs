using Phantom.Data.Extensions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.RabbitMq;

namespace Phantom.AspNetCore.Extensions;

/// <summary>
/// Central configuration options for the Phantom framework. Provides a fluent API
/// for configuring database, messaging, CQRS, validation, and resilience features.
/// </summary>
public class PhantomOptions
{
    internal PhantomDataOptions DataOptions { get; } = new();
    internal PhantomMessagingOptions MessagingOptions { get; } = new();
    internal bool UseCQRS { get; set; } = true;
    internal bool UseValidation { get; set; }

    /// <summary>
    /// Configures Phantom to use PostgreSQL as the database provider.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string. Must not be null or empty.</param>
    /// <param name="configure">An optional action to further configure data options.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
    public PhantomOptions UsePostgreSQL(string connectionString, Action<PhantomDataOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

        DataOptions.Provider = DatabaseProvider.PostgreSQL;
        DataOptions.ConnectionString = connectionString;
        configure?.Invoke(DataOptions);
        return this;
    }

    /// <summary>
    /// Configures Phantom to use SQL Server as the database provider.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string. Must not be null or empty.</param>
    /// <param name="configure">An optional action to further configure data options.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
    public PhantomOptions UseSqlServer(string connectionString, Action<PhantomDataOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));

        DataOptions.Provider = DatabaseProvider.SqlServer;
        DataOptions.ConnectionString = connectionString;
        configure?.Invoke(DataOptions);
        return this;
    }

    /// <summary>
    /// Configures Phantom to use an in-memory database (for testing/development only).
    /// </summary>
    /// <param name="configure">An optional action to further configure data options.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions UseInMemoryDatabase(Action<PhantomDataOptions>? configure = null)
    {
        DataOptions.Provider = DatabaseProvider.InMemory;
        configure?.Invoke(DataOptions);
        return this;
    }

    /// <summary>
    /// Configures Phantom to use RabbitMQ as the messaging transport for the default channel.
    /// </summary>
    /// <param name="host">The RabbitMQ host address. Must not be null or empty.</param>
    /// <param name="configure">An optional action to further configure RabbitMQ options.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="host"/> is null or empty.</exception>
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
    /// Adds a named messaging channel with the specified configuration.
    /// </summary>
    /// <param name="name">The channel name. Must not be null or empty.</param>
    /// <param name="configure">An action to configure the channel builder.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public PhantomOptions AddChannel(string name, Action<ChannelBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name must not be null or empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(configure);

        MessagingOptions.AddChannel(name, configure);
        return this;
    }

    /// <summary>
    /// Enables FluentValidation integration for command and query validation.
    /// </summary>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions UseFluentValidation()
    {
        UseValidation = true;
        return this;
    }

    /// <summary>
    /// Enables soft-delete support so entities are marked as deleted rather than physically removed.
    /// </summary>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions UseSoftDelete()
    {
        DataOptions.UseSoftDelete = true;
        return this;
    }

    /// <summary>
    /// Enables auditable entity support to automatically track created/modified timestamps and users.
    /// </summary>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions UseAuditable()
    {
        DataOptions.UseAuditable = true;
        return this;
    }

    /// <summary>
    /// Enables the outbox pattern for reliable event publishing alongside database operations.
    /// </summary>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions UseOutbox()
    {
        DataOptions.UseOutbox = true;
        MessagingOptions.UseOutboxProcessing();
        return this;
    }

    /// <summary>
    /// Routes a specific integration event type to one or more named channels.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type to route.</typeparam>
    /// <param name="channelNames">The names of the channels to route the event to.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="channelNames"/> is null or empty.</exception>
    public PhantomOptions RouteEvent<TEvent>(params string[] channelNames) where TEvent : Core.Events.IIntegrationEvent
    {
        if (channelNames is null || channelNames.Length == 0)
            throw new ArgumentException("At least one channel name must be specified.", nameof(channelNames));

        MessagingOptions.RouteEvent<TEvent>(channelNames);
        return this;
    }

    /// <summary>
    /// Enables idempotent message processing to prevent duplicate handling of messages.
    /// </summary>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    public PhantomOptions EnableIdempotency()
    {
        MessagingOptions.EnableIdempotency();
        return this;
    }

    /// <summary>
    /// Configures retry resilience for message publishing and processing.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts. Must be positive.</param>
    /// <param name="baseDelay">The base delay between retries. Defaults to 1 second if null.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRetries"/> is not positive.</exception>
    public PhantomOptions ConfigureRetry(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        if (maxRetries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be positive.");

        MessagingOptions.ConfigureRetry(maxRetries, baseDelay);
        return this;
    }

    /// <summary>
    /// Configures circuit breaker resilience for message publishing and processing.
    /// </summary>
    /// <param name="failureThreshold">The number of failures before the circuit opens. Must be positive.</param>
    /// <param name="resetTimeout">The duration before the circuit attempts to close again.</param>
    /// <returns>This <see cref="PhantomOptions"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="failureThreshold"/> is not positive.</exception>
    public PhantomOptions ConfigureCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        if (failureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be positive.");

        MessagingOptions.ConfigureCircuitBreaker(failureThreshold, resetTimeout);
        return this;
    }
}
