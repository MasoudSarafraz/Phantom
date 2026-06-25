using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.NET.HealthChecks;
using Phantom.CQRS.Extensions;
using Phantom.Data.Extensions;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.Kafka;
using Phantom.Messaging.RabbitMq;
using System.Reflection;

namespace Phantom.NET.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantom(
        this IServiceCollection services,
        Assembly assembly,
        Action<PhantomOptions>? configure = null)
    {
        return services.AddPhantom(new[] { assembly }, configure);
    }

    public static IServiceCollection AddPhantom(
        this IServiceCollection services,
        Assembly[] assemblies,
        Action<PhantomOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));

        var options = new PhantomOptions();
        configure?.Invoke(options);

        return services.AddPhantomCore(assemblies, options);
    }

    public static IServiceCollection AddPhantom(
        this IServiceCollection services,
        Assembly assembly,
        IConfiguration configuration,
        Action<PhantomOptions>? configure = null)
    {
        return services.AddPhantom(new[] { assembly }, configuration, configure);
    }

    public static IServiceCollection AddPhantom(
        this IServiceCollection services,
        Assembly[] assemblies,
        IConfiguration configuration,
        Action<PhantomOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(configuration);

        if (assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));

        var options = new PhantomOptions();
        var section = configuration.GetSection(PhantomConfiguration.SectionName);

        if (section.Exists())
        {
            ApplyConfiguration(options, section);
        }

        configure?.Invoke(options);

        return services.AddPhantomCore(assemblies, options);
    }

    private static void ApplyConfiguration(PhantomOptions options, IConfiguration section)
    {
        var dbSection = section.GetSection("Database");
        if (dbSection.Exists())
        {
            var provider = dbSection["Provider"];
            var connectionString = dbSection["ConnectionString"];
            var migrationsAssembly = dbSection["MigrationsAssembly"];
            var useSoftDelete = dbSection.GetValue<bool?>("UseSoftDelete") ?? false;
            var useAuditable = dbSection.GetValue<bool?>("UseAuditable") ?? false;

            if (!string.IsNullOrWhiteSpace(provider))
            {
                var normalizedProvider = provider.ToLowerInvariant();
                if (normalizedProvider is "postgresql" or "postgres" or "npgsql")
                {
                    if (!string.IsNullOrWhiteSpace(connectionString))
                        options.UsePostgreSQL(connectionString);
                    else
                        options.DataOptions.Provider = DatabaseProvider.PostgreSQL;
                }
                else if (normalizedProvider is "sqlserver" or "mssql" or "sql")
                {
                    if (!string.IsNullOrWhiteSpace(connectionString))
                        options.UseSqlServer(connectionString);
                    else
                        options.DataOptions.Provider = DatabaseProvider.SqlServer;
                }
                else if (normalizedProvider is "inmemory" or "in-memory" or "memory")
                {
                    options.UseInMemoryDatabase();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unknown database provider '{provider}'. Supported: PostgreSQL, SqlServer, InMemory.");
                }
            }

            if (useSoftDelete) options.UseSoftDelete();
            if (useAuditable) options.UseAuditable();
            if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                options.DataOptions.MigrationsAssembly = migrationsAssembly;
        }

        var featuresSection = section.GetSection("Features");
        if (featuresSection.Exists())
        {
            var useOutbox = featuresSection.GetValue<bool?>("UseOutbox");
            var useIdempotency = featuresSection.GetValue<bool?>("UseIdempotency");
            var useFluentValidation = featuresSection.GetValue<bool?>("UseFluentValidation");
            var useCqrs = featuresSection.GetValue<bool?>("UseCQRS");

            if (useOutbox == true) options.UseOutbox();
            else if (useOutbox == false) options.DisableOutbox();

            if (useIdempotency == true) options.EnableIdempotency();

            if (useFluentValidation == true) options.UseFluentValidation();

            if (useCqrs == false) options.UseCQRS = false;
        }

        var messagingSection = section.GetSection("Messaging");
        if (messagingSection.Exists())
        {
            var channelsSection = messagingSection.GetSection("Channels");
            if (channelsSection.Exists())
            {
                foreach (var channelSection in channelsSection.GetChildren())
                {
                    var channelName = channelSection.Key;
                    var type = channelSection["Type"] ?? "InMemory";
                    var normalizedType = type.ToLowerInvariant();

                    if (normalizedType is "rabbitmq" or "rabbit")
                    {
                        var rabbitSection = channelSection.GetSection("RabbitMq");
                        var rabbitOptions = new RabbitMqOptions();
                        if (rabbitSection.Exists())
                        {
                            rabbitSection.Bind(rabbitOptions);
                        }
                        else
                        {
                            var host = channelSection["Host"];
                            if (!string.IsNullOrWhiteSpace(host)) rabbitOptions.Host = host;
                            var port = channelSection["Port"];
                            if (int.TryParse(port, out var p)) rabbitOptions.Port = p;
                            var user = channelSection["Username"];
                            if (!string.IsNullOrWhiteSpace(user)) rabbitOptions.Username = user;
                            var pass = channelSection["Password"];
                            if (pass is not null) rabbitOptions.Password = pass;
                            var vhost = channelSection["VirtualHost"];
                            if (!string.IsNullOrWhiteSpace(vhost)) rabbitOptions.VirtualHost = vhost;
                            var exchange = channelSection["Exchange"];
                            if (!string.IsNullOrWhiteSpace(exchange)) rabbitOptions.Exchange = exchange;
                        }
                        rabbitOptions.Validate();

                        options.AddChannel(channelName, c => c.UseRabbitMq(r =>
                        {
                            r.Host = rabbitOptions.Host;
                            r.Port = rabbitOptions.Port;
                            r.Username = rabbitOptions.Username;
                            r.Password = rabbitOptions.Password;
                            r.VirtualHost = rabbitOptions.VirtualHost;
                            r.Exchange = rabbitOptions.Exchange;
                            r.ConsumerGroup = rabbitOptions.ConsumerGroup;
                            r.Durable = rabbitOptions.Durable;
                            r.AutoDelete = rabbitOptions.AutoDelete;
                            r.PrefetchCount = rabbitOptions.PrefetchCount;
                            r.SslOptions = rabbitOptions.SslOptions;
                            r.RequestedHeartbeat = rabbitOptions.RequestedHeartbeat;
                            r.ClientProvidedName = rabbitOptions.ClientProvidedName;
                            r.RequestedConnectionTimeout = rabbitOptions.RequestedConnectionTimeout;
                            r.SocketReadTimeout = rabbitOptions.SocketReadTimeout;
                            r.SocketWriteTimeout = rabbitOptions.SocketWriteTimeout;
                            r.ContinuationTimeout = rabbitOptions.ContinuationTimeout;
                            r.AutomaticRecoveryEnabled = rabbitOptions.AutomaticRecoveryEnabled;
                            r.TopologyRecoveryEnabled = rabbitOptions.TopologyRecoveryEnabled;
                            r.NetworkRecoveryInterval = rabbitOptions.NetworkRecoveryInterval;
                            r.DeadLetterExchange = rabbitOptions.DeadLetterExchange;
                            r.MessageTtl = rabbitOptions.MessageTtl;
                        }));
                    }
                    else if (normalizedType is "kafka")
                    {
                        var kafkaSection = channelSection.GetSection("Kafka");
                        var kafkaOptions = new KafkaOptions();
                        if (kafkaSection.Exists())
                        {
                            kafkaSection.Bind(kafkaOptions);
                        }
                        else
                        {
                            var servers = channelSection["BootstrapServers"];
                            if (!string.IsNullOrWhiteSpace(servers)) kafkaOptions.BootstrapServers = servers;
                            var groupId = channelSection["GroupId"];
                            if (!string.IsNullOrWhiteSpace(groupId)) kafkaOptions.GroupId = groupId;
                            var topicPrefix = channelSection["TopicPrefix"];
                            if (!string.IsNullOrWhiteSpace(topicPrefix)) kafkaOptions.TopicPrefix = topicPrefix;
                        }
                        kafkaOptions.Validate();

                        options.AddChannel(channelName, c => c.UseKafka(k =>
                        {
                            k.BootstrapServers = kafkaOptions.BootstrapServers;
                            k.GroupId = kafkaOptions.GroupId;
                            k.TopicPrefix = kafkaOptions.TopicPrefix;
                            k.AutoOffsetReset = kafkaOptions.AutoOffsetReset;
                            k.EnableAutoCommit = kafkaOptions.EnableAutoCommit;
                            k.AutoCommitInterval = kafkaOptions.AutoCommitInterval;
                            k.MaxPollInterval = kafkaOptions.MaxPollInterval;
                            k.SessionTimeout = kafkaOptions.SessionTimeout;
                            k.Acks = kafkaOptions.Acks;
                            k.MessageRetries = kafkaOptions.MessageRetries;
                            k.RetryBackoff = kafkaOptions.RetryBackoff;
                            k.BatchSize = kafkaOptions.BatchSize;
                            k.LingerMs = kafkaOptions.LingerMs;
                            k.MessageMaxBytes = kafkaOptions.MessageMaxBytes;
                            k.SecurityProtocol = kafkaOptions.SecurityProtocol;
                            k.SaslMechanism = kafkaOptions.SaslMechanism;
                            k.SaslUsername = kafkaOptions.SaslUsername;
                            k.SaslPassword = kafkaOptions.SaslPassword;
                            k.EnableDeadLetterTopic = kafkaOptions.EnableDeadLetterTopic;
                        }));
                    }
                    else
                    {
                        options.AddChannel(channelName, c => c.UseInMemory());
                    }
                }
            }

            var routingSection = messagingSection.GetSection("EventRouting");
            if (routingSection.Exists())
            {
                foreach (var routeSection in routingSection.GetChildren())
                {
                    var eventTypeName = routeSection.Key;
                    var channelNames = routeSection.Get<string[]>() ?? Array.Empty<string>();
                    if (channelNames.Length == 0)
                    {
                        var single = routeSection.Get<string>();
                        if (!string.IsNullOrWhiteSpace(single))
                            channelNames = new[] { single };
                    }

                    var eventType = ResolveEventType(eventTypeName);
                    if (eventType is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not resolve event type '{eventTypeName}' configured in Phantom:Messaging:EventRouting. " +
                            "Ensure the type name is a valid fully-qualified name or that the assembly is loaded.");
                    }

                    var method = typeof(PhantomOptions)
                        .GetMethods()
                        .First(m => m.Name == nameof(PhantomOptions.RouteEvent) && m.GetParameters()[0].ParameterType == typeof(string[]))
                        .MakeGenericMethod(eventType);

                    method.Invoke(options, new object[] { channelNames });
                }
            }

            var retrySection = messagingSection.GetSection("Retry");
            if (retrySection.Exists())
            {
                var maxRetries = retrySection.GetValue<int?>("MaxRetries");
                var baseDelay = retrySection.GetValue<TimeSpan?>("BaseDelay");
                if (maxRetries.HasValue)
                    options.ConfigureRetry(maxRetries.Value, baseDelay);
            }

            var cbSection = messagingSection.GetSection("CircuitBreaker");
            if (cbSection.Exists())
            {
                var failureThreshold = cbSection.GetValue<int?>("FailureThreshold");
                var resetTimeout = cbSection.GetValue<TimeSpan?>("ResetTimeout");
                if (failureThreshold.HasValue)
                    options.ConfigureCircuitBreaker(failureThreshold.Value, resetTimeout);
            }

            var outboxBatchSize = messagingSection.GetValue<int?>("OutboxBatchSize");
            var outboxPollingInterval = messagingSection.GetValue<TimeSpan?>("OutboxPollingInterval");
            var throwIfNoChannelFound = messagingSection.GetValue<bool?>("ThrowIfNoChannelFound");
            if (throwIfNoChannelFound == true)
                options.MessagingOptions.ThrowIfNoChannelFound = true;
            if (outboxBatchSize.HasValue || outboxPollingInterval.HasValue)
            {
                options.MessagingOptions.UseOutboxProcessing(
                    outboxBatchSize ?? 100,
                    outboxPollingInterval ?? TimeSpan.FromSeconds(5));
            }
        }
    }

    private static Type? ResolveEventType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        var t = Type.GetType(typeName);
        if (t is not null) return t;

        var typeNameOnly = typeName.Split(',')[0].Trim();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var found = assembly.GetType(typeNameOnly);
                if (found is not null) return found;
            }
            catch { }
        }

        var byName = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        return byName;
    }

    private static IServiceCollection AddPhantomCore(
        this IServiceCollection services,
        Assembly[] assemblies,
        PhantomOptions options)
    {
        options.Validate();

        if (string.IsNullOrWhiteSpace(options.DataOptions.ConnectionString) &&
            options.DataOptions.Provider != DatabaseProvider.InMemory)
        {
            throw new InvalidOperationException(
                "No database provider configured. Call UsePostgreSQL(), UseSqlServer(), or UseInMemoryDatabase() " +
                "on PhantomOptions before calling AddPhantom(). Or set 'Phantom:Database:Provider' and " +
                "'Phantom:Database:ConnectionString' in appsettings.json.");
        }

        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !(t.IsGenericTypeDefinition && t.ContainsGenericParameters))
                .Select(t => new
                {
                    Type = t,
                    Interfaces = t.GetInterfaces().Where(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(Core.Events.IDomainEventHandler<>))
                })
                .Where(x => x.Interfaces.Any());

            foreach (var handler in handlerTypes)
            {
                foreach (var iface in handler.Interfaces)
                {
                    services.AddScoped(iface, handler.Type);
                }
            }
        }

        if (options.UseCQRS)
        {
            foreach (var assembly in assemblies)
            {
                services.AddPhantomCQRS(assembly);
            }
        }

        if (options.UseValidation)
        {
            services.AddPhantomValidation();
        }

        services.AddPhantomData(d =>
        {
            d.ConnectionString = options.DataOptions.ConnectionString;
            d.Provider = options.DataOptions.Provider;
            d.UseSoftDelete = options.DataOptions.UseSoftDelete;
            d.UseAuditable = options.DataOptions.UseAuditable;
            d.UseOutbox = options.DataOptions.UseOutbox;
            d.UseIdempotency = options.DataOptions.UseIdempotency;
            d.ConfigureDbContext = options.DataOptions.ConfigureDbContext;
        });

        services.AddPhantomMessaging(assemblies, m =>
        {
            foreach (var kvp in options.MessagingOptions.ChannelBuilders)
            {
                m.AddChannel(kvp.Key, kvp.Value);
            }

            if (options.MessagingOptions.UseOutbox)
            {
                m.UseOutboxProcessing(
                    options.MessagingOptions.OutboxBatchSize,
                    options.MessagingOptions.OutboxPollingInterval);
            }

            if (options.MessagingOptions.UseIdempotency)
            {
                m.EnableIdempotency();
            }
        });

        return services;
    }

    public static IHealthChecksBuilder AddPhantomBrokerHealthCheck(
        this IHealthChecksBuilder builder,
        string channelName,
        string? name = null)
    {
        builder.Services.AddSingleton<BrokerHealthCheck>(
            sp => new BrokerHealthCheck(channelName, sp.GetRequiredService<IServiceScopeFactory>()));
        builder.AddCheck<BrokerHealthCheck>(
            name ?? $"phantom-broker-{channelName}",
            tags: new[] { "phantom", "broker" });
        return builder;
    }

    public static IHealthChecksBuilder AddPhantomDatabaseHealthCheck(
        this IHealthChecksBuilder builder,
        string? name = null)
    {
        return builder.AddCheck<DatabaseHealthCheck>(
            name ?? "phantom-database");
    }
}

