using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Dispatchers;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;
using System.Reflection;

namespace Phantom.CQRS.Extensions;

/// <summary>
/// Extension methods for registering Phantom CQRS services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phantom CQRS dispatcher, logging pipeline behavior, and all
    /// command/query handlers found in the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for handler implementations.
    /// All non-abstract types implementing <see cref="ICommandHandler{TCommand}"/>,
    /// <see cref="ICommandHandler{TCommand,TResult}"/>, or <see cref="IQueryHandler{TQuery,TResult}"/>
    /// will be registered as scoped services.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assemblies"/> is <c>null</c>.</exception>
    public static IServiceCollection AddPhantomCQRS(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        services.TryAddScoped<IDispatcher, Dispatcher>();
        services.AddScoped(typeof(IPipelineBehavior<>), typeof(LoggingPipelineBehavior<>));

        foreach (var assembly in assemblies)
        {
            if (assembly is null)
                continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            var handlerTypes = types
                .Where(t => !t.IsAbstract && !(t.IsGenericTypeDefinition && t.ContainsGenericParameters))
                .Select(t => new
                {
                    Type = t,
                    Interfaces = t.GetInterfaces()
                        .Where(i => i.IsGenericType && (
                            i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                            i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                            i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
                })
                .Where(x => x.Interfaces.Any());

            foreach (var handler in handlerTypes)
                foreach (var iface in handler.Interfaces)
                    services.AddScoped(iface, handler.Type);
        }

        return services;
    }

    /// <summary>
    /// Registers the validation pipeline behavior for Phantom CQRS.
    /// FluentValidation validators must be registered separately (e.g. via
    /// <c>AddValidatorsFromAssembly</c>).
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPhantomValidation(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<>), typeof(ValidationPipelineBehavior<>));
        return services;
    }
}
