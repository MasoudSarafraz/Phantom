using RabbitMQ.Client;

namespace Phantom.Messaging.RabbitMq;

/// <summary>
/// Configuration options for the RabbitMQ transport.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ broker hostname. Defaults to "localhost".
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ broker port. Defaults to 5672.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the username for authentication. Defaults to "guest".
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the password for authentication. Defaults to "guest".
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host. Defaults to "/".
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the exchange name for publishing events. Defaults to "phantom".
    /// </summary>
    public string Exchange { get; set; } = "phantom";

    /// <summary>
    /// Gets or sets the consumer group name used as a prefix for queue names. Defaults to "phantom-consumer".
    /// </summary>
    public string ConsumerGroup { get; set; } = "phantom-consumer";

    /// <summary>
    /// Gets or sets a value indicating whether queues and exchanges should be declared as durable. Defaults to true.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether queues should be auto-deleted when no longer in use. Defaults to false.
    /// </summary>
    public bool AutoDelete { get; set; } = false;

    /// <summary>
    /// Gets or sets the prefetch count for consumers, controlling how many unacknowledged messages
    /// can be delivered simultaneously. Defaults to 10.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Gets or sets the SSL/TLS options for secure connections, or null to use an unencrypted connection.
    /// </summary>
    public SslOption? SslOptions { get; set; }

    /// <summary>
    /// Gets or sets the requested heartbeat interval for the connection.
    /// If the broker does not receive a heartbeat within this interval, the connection is considered lost.
    /// Defaults to 60 seconds.
    /// </summary>
    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the client-provided name for the connection, which appears in the RabbitMQ management UI.
    /// Useful for identifying connections in multi-service environments.
    /// </summary>
    public string? ClientProvidedName { get; set; }

    /// <summary>
    /// Validates this options instance and throws an <see cref="InvalidOperationException"/> if any required settings are invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when any configuration value is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("RabbitMQ Host must not be empty or whitespace.");

        if (Port is < 1 or > 65535)
            throw new InvalidOperationException($"RabbitMQ Port must be between 1 and 65535, got {Port}.");

        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("RabbitMQ Username must not be empty or whitespace.");

        if (Password is null)
            throw new InvalidOperationException("RabbitMQ Password must not be null.");

        if (string.IsNullOrWhiteSpace(Exchange))
            throw new InvalidOperationException("RabbitMQ Exchange must not be empty or whitespace.");

        if (string.IsNullOrWhiteSpace(ConsumerGroup))
            throw new InvalidOperationException("RabbitMQ ConsumerGroup must not be empty or whitespace.");

        if (RequestedHeartbeat <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ RequestedHeartbeat must be positive, got {RequestedHeartbeat}.");
    }
}
