using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Messaging.RabbitMq;
using System.Reflection;

namespace Phantom.Messaging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomMessaging(this IServiceCollection services, Assembly assembly, Action<PhantomMessagingOptions>? configure = null)
    {
        var options = new PhantomMessagingOptions();
        if (!options.ChannelBuilders.Any()) options.AddChannel("default", c => c.UseInMemory());
        configure?.Invoke(options);

        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IChannelRegistry>(sp => new ChannelRegistry(sp.GetRequiredService<ILogger<ChannelRegistry>>()));
        services.AddScoped<IEventPublisher, EventPublisher>();

        services.AddSingleton(sp =>
        {
            var registry = (ChannelRegistry)sp.GetRequiredService<IChannelRegistry>();
            foreach (var (name, builderAction) in options.ChannelBuilders)
            {
                var builder = new ChannelBuilder(name);
                builderAction(builder);
                IChannelAdapter adapter;
                if (builder.AdapterType == typeof(RabbitMqChannelAdapter))
                    adapter = new RabbitMqChannelAdapter(name, (RabbitMqOptions)builder.AdapterOptions!, sp.GetRequiredService<IMessageSerializer>(), sp, sp.GetRequiredService<ILogger<RabbitMqChannelAdapter>>());
                else
                    adapter = new InMemoryChannelAdapter(name, sp, sp.GetRequiredService<ILogger<InMemoryChannelAdapter>>());
                registry.Register(name, adapter);
            }
            foreach (var (eventType, channels) in options.EventChannelMappings)
            {
                var mapMethod = typeof(IChannelRegistry).GetMethod("MapEventToChannel")!;
                var genericMethod = mapMethod.MakeGenericMethod(eventType);
                foreach (var channel in channels) genericMethod.Invoke(registry, new object[] { channel });
            }
            return registry;
        });

        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !(t.IsGenericTypeDefinition && t.ContainsGenericParameters))
            .Select(t => new { Type = t, Interfaces = t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)) })
            .Where(x => x.Interfaces.Any());
        foreach (var handler in handlerTypes) foreach (var iface in handler.Interfaces) services.AddScoped(iface, handler.Type);

        if (options.UseOutbox) services.AddHostedService<OutboxProcessor>();
        return services;
    }
}
