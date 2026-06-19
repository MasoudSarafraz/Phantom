using Confluent.Kafka;

namespace Phantom.Messaging.Kafka;

public class KafkaOptions
{
    /// <summary>
    /// Comma-separated list of Kafka bootstrap servers (e.g., "localhost:9092").
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Prefix added to all topic names. Topic name = {TopicPrefix}.{EventTypeName}.
    /// </summary>
    public string TopicPrefix { get; set; } = "phantom";

    /// <summary>
    /// Kafka consumer group ID. Each microservice should use a unique group ID.
    /// </summary>
    public string GroupId { get; set; } = "phantom-consumer";

    /// <summary>
    /// Auto-commit interval for consumer offsets. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan AutoCommitInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Auto-offset-reset policy: "earliest" or "latest". Defaults to "earliest"
    /// so new consumer groups start from the beginning of the topic.
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>
    /// Whether to enable auto-commit. When disabled, offsets are committed manually
    /// after successful event processing (at-least-once semantics).
    /// Defaults to false for reliability.
    /// </summary>
    public bool EnableAutoCommit { get; set; } = false;

    /// <summary>
    /// Maximum time between consecutive consumer polls before the consumer is considered dead.
    /// </summary>
    public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Session timeout for the consumer. If no heartbeat is received within this time,
    /// the broker will remove the consumer from the group.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Acks configuration for the producer: "none", "leader", or "all".
    /// Defaults to "all" for strongest durability guarantee.
    /// </summary>
    public string Acks { get; set; } = "all";

    /// <summary>
    /// Number of retries for the producer. Defaults to int.MaxValue for maximum reliability.
    /// </summary>
    public int MessageRetries { get; set; } = int.MaxValue;

    /// <summary>
    /// Delay between producer retries. Defaults to 100ms.
    /// </summary>
    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum number of messages batched together before sending. Higher values
    /// improve throughput but increase latency.
    /// </summary>
    public int BatchSize { get; set; } = 16384;

    /// <summary>
    /// Time to wait before sending a batch. Higher values improve batching efficiency.
    /// </summary>
    public TimeSpan LingerMs { get; set; } = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Maximum size of a message in bytes. Defaults to 1 MB.
    /// </summary>
    public int MessageMaxBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Security protocol: "plaintext", "ssl", "sasl_plaintext", "sasl_ssl".
    /// </summary>
    public string SecurityProtocol { get; set; } = "plaintext";

    /// <summary>
    /// SASL mechanism: "plain", "scram-sha-256", "scram-sha-512", etc.
    /// </summary>
    public string? SaslMechanism { get; set; }

    /// <summary>
    /// SASL username for authentication.
    /// </summary>
    public string? SaslUsername { get; set; }

    /// <summary>
    /// SASL password for authentication.
    /// </summary>
    public string? SaslPassword { get; set; }

    /// <summary>
    /// Whether to publish failed messages to a dead-letter topic.
    /// Dead-letter topic name format: {TopicPrefix}.dead-letter.{OriginalTopic}.
    /// Defaults to true for production reliability.
    /// </summary>
    public bool EnableDeadLetterTopic { get; set; } = true;

    /// <summary>
    /// Validates the options and throws if any required setting is invalid.
    /// This method is called during DI registration to fail fast on misconfiguration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
            throw new InvalidOperationException("Kafka BootstrapServers must not be empty or whitespace.");

        if (string.IsNullOrWhiteSpace(GroupId))
            throw new InvalidOperationException("Kafka GroupId must not be empty or whitespace.");

        if (string.IsNullOrWhiteSpace(TopicPrefix))
            throw new InvalidOperationException("Kafka TopicPrefix must not be empty or whitespace.");

        if (AutoOffsetReset is not ("earliest" or "latest"))
            throw new InvalidOperationException($"Kafka AutoOffsetReset must be 'earliest' or 'latest', got '{AutoOffsetReset}'.");

        if (Acks is not ("none" or "leader" or "all"))
            throw new InvalidOperationException($"Kafka Acks must be 'none', 'leader', or 'all', got '{Acks}'.");

        if (!Enum.TryParse<SecurityProtocol>(SecurityProtocol, ignoreCase: true, out _))
            throw new InvalidOperationException($"Kafka SecurityProtocol must be one of: {string.Join(", ", Enum.GetNames<SecurityProtocol>())}, got '{SecurityProtocol}'.");

        if (!string.IsNullOrWhiteSpace(SaslMechanism) && !Enum.TryParse<SaslMechanism>(SaslMechanism, ignoreCase: true, out _))
            throw new InvalidOperationException($"Kafka SaslMechanism must be one of: {string.Join(", ", Enum.GetNames<SaslMechanism>())}, got '{SaslMechanism}'.");
    }
}
