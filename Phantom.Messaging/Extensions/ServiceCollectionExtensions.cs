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

/// <summary>
/// Extension methods for registering the Phantom messaging system with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Phantom messaging system to the service collection, including channel adapters,
    /// event routing, handler registration, and optional outbox processing.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="assemblies">One or more assemblies to scan for <see cref="IIntegrationEventHandler{T}"/> implementations.</param>
    /// <param name="configure">An optional action to configure the messaging options.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is null.</exception>
    public static IServiceCollection AddPhantomMessaging(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return AddPhantomMessaging(services, assemblies, configure: null);
    }

    /// <summary>
    /// Adds the Phantom messaging system to the service collection, including channel adapters,
    /// event routing, handler registration, and optional outbox processing.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="assemblies">One or more assemblies to scan for <see cref="IIntegrationEventHandler{T}"/> implementations.</param>
    /// <param name="configure">An optional action to configure the messaging options.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is null.</exception>
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

        // Register the message serializer
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

        // Register the channel registry (single registration, no duplicates)
        services.AddSingleton<IChannelRegistry, ChannelRegistry>();

        // Register the event publisher with the ThrowIfNoChannelFound option
        services.AddScoped<IEventPublisher>(sp =>
            new EventPublisher(
                sp.GetRequiredService<IChannelRegistry>(),
                sp.GetRequiredService<ILogger<EventPublisher>>(),
                options.ThrowIfNoChannelFound));

        // Build and register channel adapters, and set up event-to-channel mappings
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

            // Map events to channels using the non-generic overload (no reflection needed)
            foreach (var (eventType, channels) in options.EventChannelMappings)
            {
                foreach (var channel in channels)
                {
                    registry.MapEventToChannel(eventType, channel);
                }
            }

            return registry;
        });

        // Register event handlers from the provided assemblies
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

        // Register resilience policies if configured
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

        // Register the hosted service that starts/stops all channel adapters
        services.AddHostedService<ChannelAdapterHostedService>();

        // Register outbox processing if enabled
        if (options.UseOutbox)
        {
            // IOutboxMessageRepository must be provided by the application or a persistence package.
            // If not already registered, add a placeholder that throws a clear error message at resolution time.
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
