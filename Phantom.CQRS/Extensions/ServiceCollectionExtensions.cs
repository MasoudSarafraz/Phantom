using Microsoft.Extensions.DependencyInjection;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Dispatchers;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;
using System.Reflection;

namespace Phantom.CQRS.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPhantomCQRS(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped(typeof(IPipelineBehavior<>), typeof(LoggingPipelineBehavior<>));

        var handlerTypes = assembly.GetTypes()
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

        return services;
    }

    public static IServiceCollection AddPhantomValidation(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<>), typeof(ValidationPipelineBehavior<>));
        return services;
    }
}
