using RabbitMQ.Client;

namespace Phantom.Messaging.RabbitMq;

public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string Exchange { get; set; } = "phantom";

    public string ConsumerGroup { get; set; } = "phantom-consumer";

    public bool Durable { get; set; } = true;

    public bool AutoDelete { get; set; } = false;

    public ushort PrefetchCount { get; set; } = 10;

    public SslOption? SslOptions { get; set; }

    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan RequestedConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan SocketReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan SocketWriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public string? ClientProvidedName { get; set; }

    public bool AutomaticRecoveryEnabled { get; set; } = true;

    public bool TopologyRecoveryEnabled { get; set; } = true;

    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

    public string? DeadLetterExchange { get; set; }

    public TimeSpan MessageTtl { get; set; } = TimeSpan.Zero;

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

        if (RequestedConnectionTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ RequestedConnectionTimeout must be positive, got {RequestedConnectionTimeout}.");

        if (SocketReadTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ SocketReadTimeout must be positive, got {SocketReadTimeout}.");

        if (SocketWriteTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ SocketWriteTimeout must be positive, got {SocketWriteTimeout}.");

        if (ContinuationTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ ContinuationTimeout must be positive, got {ContinuationTimeout}.");

        if (NetworkRecoveryInterval <= TimeSpan.Zero)
            throw new InvalidOperationException($"RabbitMQ NetworkRecoveryInterval must be positive, got {NetworkRecoveryInterval}.");
    }
}
