using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Dispatchers;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;
using System.Reflection;

namespace Phantom.CQRS.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomCQRS(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var generatedDispatcherType = Type.GetType("Phantom.CQRS.Dispatchers.GeneratedDispatcher, Phantom.CQRS.SourceGenerator");
        if (generatedDispatcherType is not null)
        {
            services.TryAddScoped(typeof(IDispatcher), generatedDispatcherType);
        }
        else
        {
            services.TryAddScoped<IDispatcher, Dispatcher>();
        }

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

    public static IServiceCollection AddPhantomValidation(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<>), typeof(ValidationPipelineBehavior<>));
        return services;
    }
}
