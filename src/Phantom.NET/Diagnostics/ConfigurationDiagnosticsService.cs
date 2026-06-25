using Microsoft.Extensions.Configuration;
using Phantom.Data.Extensions;

namespace Phantom.NET.Diagnostics;

public class ConfigurationDiagnosticsService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ConfigurationDiagnosticsService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public object GetConfigurationSnapshot()
    {
        var result = new Dictionary<string, object?>();

        var phantomSection = _configuration.GetSection("Phantom");
        var dbSection = phantomSection.GetSection("Database");
        var featuresSection = phantomSection.GetSection("Features");
        var messagingSection = phantomSection.GetSection("Messaging");

        result["useCQRS"] = featuresSection.GetValue<bool?>("UseCQRS") ?? true;
        result["useValidation"] = featuresSection.GetValue<bool?>("UseFluentValidation") ?? false;

        result["database"] = new
        {
            provider = dbSection["Provider"] ?? "InMemory",
            useSoftDelete = featuresSection.GetValue<bool?>("UseSoftDelete") ?? false,
            useAuditable = featuresSection.GetValue<bool?>("UseAuditable") ?? false,
            useOutbox = featuresSection.GetValue<bool?>("UseOutbox") ?? true,
            useIdempotency = featuresSection.GetValue<bool?>("UseIdempotency") ?? false,
            hasConnectionString = !string.IsNullOrWhiteSpace(dbSection["ConnectionString"]),
            hasCustomDbContextConfig = false
        };

        var channelsSection = messagingSection.GetSection("Channels");
        var channelNames = channelsSection.GetChildren().Select(c => c.Key).ToList();

        result["messaging"] = new
        {
            channelCount = channelNames.Count,
            channelNames,
            eventRoutingCount = messagingSection.GetSection("EventRouting").GetChildren().Count(),
            useOutbox = featuresSection.GetValue<bool?>("UseOutbox") ?? true,
            useIdempotency = featuresSection.GetValue<bool?>("UseIdempotency") ?? false,
            outboxBatchSize = messagingSection.GetValue<int?>("OutboxBatchSize") ?? 100,
            outboxPollingInterval = messagingSection.GetValue<TimeSpan?>("OutboxPollingInterval") ?? TimeSpan.FromSeconds(5),
            throwIfNoChannelFound = messagingSection.GetValue<bool?>("ThrowIfNoChannelFound") ?? false,
            retry = messagingSection.GetSection("Retry").Exists() ? new
            {
                maxRetries = messagingSection.GetSection("Retry").GetValue<int?>("MaxRetries") ?? 3,
                baseDelay = messagingSection.GetSection("Retry").GetValue<TimeSpan?>("BaseDelay") ?? TimeSpan.FromSeconds(1)
            } : null,
            circuitBreaker = messagingSection.GetSection("CircuitBreaker").Exists() ? new
            {
                failureThreshold = messagingSection.GetSection("CircuitBreaker").GetValue<int?>("FailureThreshold") ?? 5,
                resetTimeout = messagingSection.GetSection("CircuitBreaker").GetValue<TimeSpan?>("ResetTimeout") ?? TimeSpan.FromSeconds(30)
            } : null
        };

        var registeredServices = new Dictionary<string, bool>();
        foreach (var (name, type) in new[]
        {
            ("PhantomDbContext", typeof(Phantom.Data.EfCore.PhantomDbContext)),
            ("IUnitOfWork", typeof(Phantom.Core.Services.IUnitOfWork)),
            ("IDomainEventDispatcher", typeof(Phantom.Core.Events.IDomainEventDispatcher)),
            ("IChannelRegistry", typeof(Phantom.Messaging.Abstractions.IChannelRegistry)),
            ("IEventPublisher", typeof(Phantom.Messaging.Abstractions.IEventPublisher)),
            ("IOutboxMessageRepository", typeof(Phantom.Infrastructure.Abstractions.Outbox.IOutboxMessageRepository)),
            ("IIdempotencyTracker", typeof(Phantom.Infrastructure.Abstractions.Idempotency.IIdempotencyTracker)),
            ("IResiliencePipeline", typeof(Phantom.Messaging.Abstractions.IResiliencePipeline)),
            ("IDispatcher", typeof(Phantom.CQRS.Dispatchers.IDispatcher)),
            ("ISpecificationEvaluator", typeof(Phantom.Data.Specifications.ISpecificationEvaluator))
        })
        {
            var service = _serviceProvider.GetService(type);
            registeredServices[name] = service is not null;
        }

        result["registeredServices"] = registeredServices;

        return result;
    }
}
