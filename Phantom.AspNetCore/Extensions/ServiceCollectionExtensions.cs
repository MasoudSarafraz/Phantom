using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Phantom.AspNetCore.HealthChecks;
using Phantom.CQRS.Extensions;
using Phantom.Data.Extensions;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using System.Reflection;

namespace Phantom.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering Phantom framework services with the
/// <see cref="IServiceCollection"/> DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Phantom framework services using types from the specified assembly.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="assembly">The assembly to scan for domain event handlers, command handlers, etc.</param>
    /// <param name="configure">An optional action to configure Phantom options.</param>
    /// <returns>The <see cref="IServiceCollection"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no database provider has been configured. Call one of the
    /// <c>UsePostgreSQL</c>, <c>UseSqlServer</c>, or <c>UseInMemoryDatabase</c> methods on
    /// <see cref="PhantomOptions"/> first.
    /// </exception>
    public static IServiceCollection AddPhantom(
        this IServiceCollection services,
        Assembly assembly,
        Action<PhantomOptions>? configure = null)
    {
        return services.AddPhantom(new[] { assembly }, configure);
    }

    /// <summary>
    /// Registers all Phantom framework services using types from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="assemblies">The assemblies to scan for domain event handlers, command handlers, etc.</param>
    /// <param name="configure">An optional action to configure Phantom options.</param>
    /// <returns>The <see cref="IServiceCollection"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no database provider has been configured. Call one of the
    /// <c>UsePostgreSQL</c>, <c>UseSqlServer</c>, or <c>UseInMemoryDatabase</c> methods on
    /// <see cref="PhantomOptions"/> first.
    /// </exception>
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

        // Startup validation: ensure a database provider is configured
        if (string.IsNullOrWhiteSpace(options.DataOptions.ConnectionString) &&
            options.DataOptions.Provider != DatabaseProvider.InMemory)
        {
            throw new InvalidOperationException(
                "No database provider configured. Call UsePostgreSQL(), UseSqlServer(), or UseInMemoryDatabase() " +
                "on PhantomOptions before calling AddPhantom().");
        }

        // Core — scan domain event handlers across all assemblies
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

        // CQRS
        if (options.UseCQRS)
        {
            foreach (var assembly in assemblies)
            {
                services.AddPhantomCQRS(assembly);
            }
        }

        // Validation
        if (options.UseValidation)
        {
            services.AddPhantomValidation();
        }

        // Data
        services.AddPhantomData(d =>
        {
            d.ConnectionString = options.DataOptions.ConnectionString;
            d.Provider = options.DataOptions.Provider;
            d.UseSoftDelete = options.DataOptions.UseSoftDelete;
            d.UseAuditable = options.DataOptions.UseAuditable;
            d.UseOutbox = options.DataOptions.UseOutbox;
            d.ConfigureDbContext = options.DataOptions.ConfigureDbContext;
        });

        // Messaging
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

    /// <summary>
    /// Adds a health check for the specified messaging broker channel.
    /// The health check uses <see cref="IServiceScopeFactory"/> to resolve scoped
    /// dependencies, avoiding lifetime mismatch issues.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="channelName">The name of the channel to check.</param>
    /// <param name="name">An optional custom name for the health check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for fluent chaining.</returns>
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

    /// <summary>
    /// Adds a health check for the Phantom database connectivity.
    /// The health check uses <see cref="IServiceScopeFactory"/> to resolve the scoped
    /// <see cref="PhantomDbContext"/>, avoiding lifetime mismatch issues.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">An optional custom name for the health check.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for fluent chaining.</returns>
    public static IHealthChecksBuilder AddPhantomDatabaseHealthCheck(
        this IHealthChecksBuilder builder,
        string? name = null)
    {
        return builder.AddCheck<DatabaseHealthCheck>(
            name ?? "phantom-database");
    }
}
