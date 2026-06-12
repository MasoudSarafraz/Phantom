using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Messaging.RabbitMq;
using Phantom.Messaging.Resilience;
using System.Reflection;

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

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        services.AddSingleton<IChannelRegistry, ChannelRegistry>();

        services.AddScoped<IEventPublisher>(sp =>
            new EventPublisher(
                sp.GetRequiredService<IChannelRegistry>(),
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
                IChannelAdapter adapter;
                if (builder.AdapterType == typeof(RabbitMqChannelAdapter))
                    adapter = new RabbitMqChannelAdapter(name, (RabbitMqOptions)builder.AdapterOptions!, serializer, sp, sp.GetRequiredService<ILogger<RabbitMqChannelAdapter>>());
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
                    services.AddScoped(iface, handler.Type);
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
                    sp.GetRequiredService<ILogger<OutboxProcessor>>(),
                    options.OutboxBatchSize,
                    options.OutboxPollingInterval));
        }

        return services;
    }
}
