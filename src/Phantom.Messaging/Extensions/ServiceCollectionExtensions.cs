using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Outbox;
using Phantom.Messaging.Kafka;
using Phantom.Messaging.RabbitMq;
using Phantom.Messaging.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Reflection;

using RetryPolicy        = Phantom.Messaging.Resilience.RetryPolicy;
using CircuitBreakerPolicy = Phantom.Messaging.Resilience.CircuitBreakerPolicy;
using PollyRetryStrategyOptions        = Polly.Retry.RetryStrategyOptions;
using PollyCircuitBreakerStrategyOptions = Polly.CircuitBreaker.CircuitBreakerStrategyOptions;

namespace Phantom.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomMessaging(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return AddPhantomMessaging(services, assemblies, configure: null);
    }

    public static IServiceCollection AddPhantomMessaging(
        this IServiceCollection services,
        Assembly[] assemblies,
        Action<PhantomMessagingOptions>? configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));

        var options = new PhantomMessagingOptions();
        if (!options.ChannelBuilders.Any()) options.AddChannel("default", c => c.UseInMemory());
        configure?.Invoke(options);

        services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();

        services.AddSingleton<IChannelRegistry, ChannelRegistry>();

        services.TryAddSingleton<IResiliencePipeline>(sp =>
        {
            PollyRetryStrategyOptions? retryOptions = null;
            PollyCircuitBreakerStrategyOptions? cbOptions = null;

            if (options.Retry is not null)
            {
                var retryLogger = sp.GetRequiredService<ILogger<RetryPolicy>>();
                retryOptions = new PollyRetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetries,
                    Delay = options.Retry.BaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    OnRetry = args =>
                    {
                        retryLogger.LogWarning("[Phantom] Retry attempt {Attempt}/{Max} after {Delay}ms",
                            args.AttemptNumber + 1, options.Retry.MaxRetries,
                            args.RetryDelay.TotalMilliseconds);
                        return default;
                    }
                };
            }

            if (options.CircuitBreaker is not null)
            {
                var cbLogger = sp.GetRequiredService<ILogger<CircuitBreakerPolicy>>();
                cbOptions = new PollyCircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = options.CircuitBreaker.FailureThreshold,
                    BreakDuration = options.CircuitBreaker.ResetTimeout,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    OnOpened = args =>
                    {
                        cbLogger.LogWarning("[Phantom] Circuit breaker OPENED");
                        return default;
                    },
                    OnClosed = args =>
                    {
                        cbLogger.LogInformation("[Phantom] Circuit breaker CLOSED (recovered)");
                        return default;
                    },
                    OnHalfOpened = args =>
                    {
                        cbLogger.LogInformation("[Phantom] Circuit breaker HALF-OPEN (testing)");
                        return default;
                    }
                };
            }

            if (retryOptions is null && cbOptions is null)
                return NullResiliencePipeline.Instance;

            return new CompositeResiliencePipeline(retryOptions, cbOptions);
        });

        services.AddScoped<IEventPublisher>(sp =>
            new EventPublisher(
                sp.GetRequiredService<IChannelRegistry>(),
                sp.GetRequiredService<IResiliencePipeline>(),
                sp.GetRequiredService<ILogger<EventPublisher>>(),
                options.ThrowIfNoChannelFound));

        services.AddSingleton(sp =>
        {
            var registry = (ChannelRegistry)sp.GetRequiredService<IChannelRegistry>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();

            foreach (var (name, builderAction) in options.ChannelBuilders)
            {
                var builder = new ChannelBuilder(name);
                builderAction(builder);

                if (builder.AdapterType == typeof(RabbitMqChannelAdapter))
                    ((RabbitMqOptions)builder.AdapterOptions!).Validate();
                else if (builder.AdapterType == typeof(KafkaChannelAdapter))
                    ((KafkaOptions)builder.AdapterOptions!).Validate();

                IChannelAdapter adapter;
                if (builder.AdapterType == typeof(RabbitMqChannelAdapter))
                    adapter = new RabbitMqChannelAdapter(name, (RabbitMqOptions)builder.AdapterOptions!, serializer, sp, sp.GetRequiredService<ILogger<RabbitMqChannelAdapter>>());
                else if (builder.AdapterType == typeof(KafkaChannelAdapter))
                    adapter = new KafkaChannelAdapter(name, (KafkaOptions)builder.AdapterOptions!, serializer, sp, sp.GetRequiredService<ILogger<KafkaChannelAdapter>>());
                else
                    adapter = new InMemoryChannelAdapter(name, sp, sp.GetRequiredService<ILogger<InMemoryChannelAdapter>>());
                registry.Register(name, adapter);
            }

            foreach (var (eventType, channels) in options.EventChannelMappings)
            {
                foreach (var channel in channels)
                {
                    registry.MapEventToChannel(eventType, channel);
                }
            }

            return registry;
        });

        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !(t.IsGenericTypeDefinition && t.ContainsGenericParameters))
                .Select(t => new { Type = t, Interfaces = t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)) })
                .Where(x => x.Interfaces.Any());

            foreach (var handler in handlerTypes)
            {
                foreach (var iface in handler.Interfaces)
                {
                    if (options.UseIdempotency)
                    {
                        var eventType = iface.GetGenericArguments()[0];
                        var decoratorClosedType = typeof(IdempotentIntegrationEventHandlerDecorator<>).MakeGenericType(eventType);

                        services.AddScoped(iface, sp =>
                        {
                            var inner = ActivatorUtilities.CreateInstance(sp, handler.Type);
                            return ActivatorUtilities.CreateInstance(sp, decoratorClosedType, inner);
                        });
                    }
                    else
                    {
                        services.AddScoped(iface, handler.Type);
                    }
                }
            }
        }

        if (options.Retry is not null)
        {
            services.AddSingleton(sp =>
                new RetryPolicy(
                    options.Retry.MaxRetries,
                    options.Retry.BaseDelay,
                    sp.GetRequiredService<ILogger<RetryPolicy>>()));
        }

        if (options.CircuitBreaker is not null)
        {
            services.AddSingleton(sp =>
                new CircuitBreakerPolicy(
                    options.CircuitBreaker.FailureThreshold,
                    options.CircuitBreaker.ResetTimeout,
                    sp.GetRequiredService<ILogger<CircuitBreakerPolicy>>()));
        }

        services.AddHostedService<ChannelAdapterHostedService>();

        if (options.UseOutbox)
        {
            var outboxRepoRegistered = services.Any(sd => sd.ServiceType == typeof(IOutboxMessageRepository));
            if (!outboxRepoRegistered)
            {
                services.AddSingleton<IOutboxMessageRepository>(sp =>
                    throw new InvalidOperationException(
                        "IOutboxMessageRepository is not registered. You must provide an implementation " +
                        "of IOutboxMessageRepository (e.g., via Entity Framework or another persistence library) " +
                        "when outbox processing is enabled. Register it before calling AddPhantomMessaging."));
            }

            services.AddHostedService(sp =>
                new OutboxProcessor(
                    sp,
                    sp.GetRequiredService<IMessageSerializer>(),
                    sp.GetRequiredService<IResiliencePipeline>(),
                    sp.GetRequiredService<ILogger<OutboxProcessor>>(),
                    options.OutboxBatchSize,
                    options.OutboxPollingInterval));
        }

        return services;
    }
}
