using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.AspNetCore.HealthChecks;
using Phantom.CQRS.Extensions;
using Phantom.Data.Extensions;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using System.Reflection;

namespace Phantom.AspNetCore.Extensions;

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

        if (string.IsNullOrWhiteSpace(options.DataOptions.ConnectionString) &&
            options.DataOptions.Provider != DatabaseProvider.InMemory)
        {
            throw new InvalidOperationException(
                "No database provider configured. Call UsePostgreSQL(), UseSqlServer(), or UseInMemoryDatabase() " +
                "on PhantomOptions before calling AddPhantom().");
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
