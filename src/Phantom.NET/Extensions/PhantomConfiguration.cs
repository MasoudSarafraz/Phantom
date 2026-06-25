using Phantom.Data.Extensions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.Kafka;
using Phantom.Messaging.RabbitMq;

namespace Phantom.NET.Extensions;

public class PhantomConfiguration
{
    public const string SectionName = "Phantom";

    public PhantomDatabaseConfiguration Database { get; set; } = new();

    public PhantomMessagingConfiguration Messaging { get; set; } = new();

    public PhantomFeaturesConfiguration Features { get; set; } = new();

    public bool UseCQRS { get; set; } = true;

    public bool UseFluentValidation { get; set; }
}

public class PhantomDatabaseConfiguration
{
    public string? Provider { get; set; }

    public string? ConnectionString { get; set; }

    public string? MigrationsAssembly { get; set; }

    public bool UseSoftDelete { get; set; }

    public bool UseAuditable { get; set; }

    public Action<Microsoft.EntityFrameworkCore.DbContextOptionsBuilder>? ConfigureDbContext { get; set; }

    public DatabaseProvider GetProvider() =>
        Provider?.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" or "npgsql" => DatabaseProvider.PostgreSQL,
            "sqlserver" or "mssql" or "sql" => DatabaseProvider.SqlServer,
            "inmemory" or "in-memory" or "memory" => DatabaseProvider.InMemory,
            null or "" => DatabaseProvider.InMemory,
            _ => throw new InvalidOperationException(
                $"Unknown database provider '{Provider}'. Supported: PostgreSQL, SqlServer, InMemory.")
        };
}

public class PhantomMessagingConfiguration
{
    public Dictionary<string, PhantomChannelConfiguration> Channels { get; set; } = new();

    public Dictionary<string, List<string>> EventRouting { get; set; } = new();

    public int OutboxBatchSize { get; set; } = 100;

    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public bool ThrowIfNoChannelFound { get; set; }

    public PhantomRetryConfiguration? Retry { get; set; }

    public PhantomCircuitBreakerConfiguration? CircuitBreaker { get; set; }
}

public class PhantomChannelConfiguration
{
    public string Type { get; set; } = "InMemory";

    public RabbitMqOptions? RabbitMq { get; set; }

    public KafkaOptions? Kafka { get; set; }
}

public class PhantomFeaturesConfiguration
{
    public bool UseOutbox { get; set; } = true;

    public bool UseIdempotency { get; set; }
}

public class PhantomRetryConfiguration
{
    public int MaxRetries { get; set; } = 3;

    public TimeSpan? BaseDelay { get; set; }
}

public class PhantomCircuitBreakerConfiguration
{
    public int FailureThreshold { get; set; } = 5;

    public TimeSpan? ResetTimeout { get; set; }
}
