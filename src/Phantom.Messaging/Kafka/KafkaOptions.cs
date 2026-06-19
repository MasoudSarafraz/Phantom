using Confluent.Kafka;

namespace Phantom.Messaging.Kafka;

public class KafkaOptions
{

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string TopicPrefix { get; set; } = "phantom";

    public string GroupId { get; set; } = "phantom-consumer";

    public TimeSpan AutoCommitInterval { get; set; } = TimeSpan.FromSeconds(5);

    public string AutoOffsetReset { get; set; } = "earliest";

    public bool EnableAutoCommit { get; set; } = false;

    public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public string Acks { get; set; } = "all";

    public int MessageRetries { get; set; } = int.MaxValue;

    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromMilliseconds(100);

    public int BatchSize { get; set; } = 16384;

    public TimeSpan LingerMs { get; set; } = TimeSpan.FromMilliseconds(5);

    public int MessageMaxBytes { get; set; } = 1_048_576;

    public string SecurityProtocol { get; set; } = "plaintext";

    public string? SaslMechanism { get; set; }

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }

    public bool EnableDeadLetterTopic { get; set; } = true;

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
