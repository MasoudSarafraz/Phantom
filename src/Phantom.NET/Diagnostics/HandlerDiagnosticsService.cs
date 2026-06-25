using System.Reflection;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;
using Phantom.Core.Events;
using Phantom.Core.Messaging;

namespace Phantom.NET.Diagnostics;

public class HandlerDiagnosticsService
{
    private readonly IServiceProvider _serviceProvider;

    public HandlerDiagnosticsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object GetHandlersSnapshot()
    {
        var commandHandlers = new List<object>();
        var queryHandlers = new List<object>();
        var domainEventHandlers = new List<object>();
        var integrationEventHandlers = new List<object>();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException) { continue; }

            foreach (var t in types)
            {
                if (t.IsAbstract || t.IsInterface) continue;
                if (t.Namespace?.StartsWith("Phantom.") == true) continue;
                if (t.FullName?.StartsWith("Microsoft.") == true) continue;
                if (t.FullName?.StartsWith("System.") == true) continue;

                foreach (var iface in t.GetInterfaces())
                {
                    if (!iface.IsGenericType) continue;

                    var genericDef = iface.GetGenericTypeDefinition();
                    var args = iface.GetGenericArguments();

                    if (genericDef == typeof(ICommandHandler<>))
                    {
                        commandHandlers.Add(new
                        {
                            handlerType = t.FullName!,
                            commandType = args[0].FullName!,
                            assembly = t.Assembly.GetName().Name
                        });
                    }
                    else if (genericDef == typeof(ICommandHandler<,>))
                    {
                        commandHandlers.Add(new
                        {
                            handlerType = t.FullName!,
                            commandType = args[0].FullName!,
                            resultType = args[1].FullName!,
                            assembly = t.Assembly.GetName().Name
                        });
                    }
                    else if (genericDef == typeof(IQueryHandler<,>))
                    {
                        queryHandlers.Add(new
                        {
                            handlerType = t.FullName!,
                            queryType = args[0].FullName!,
                            resultType = args[1].FullName!,
                            assembly = t.Assembly.GetName().Name
                        });
                    }
                    else if (genericDef == typeof(IDomainEventHandler<>))
                    {
                        domainEventHandlers.Add(new
                        {
                            handlerType = t.FullName!,
                            domainEventType = args[0].FullName!,
                            assembly = t.Assembly.GetName().Name
                        });
                    }
                    else if (genericDef == typeof(IIntegrationEventHandler<>))
                    {
                        integrationEventHandlers.Add(new
                        {
                            handlerType = t.FullName!,
                            integrationEventType = args[0].FullName!,
                            assembly = t.Assembly.GetName().Name
                        });
                    }
                }
            }
        }

        return new
        {
            commandHandlersCount = commandHandlers.Count,
            queryHandlersCount = queryHandlers.Count,
            domainEventHandlersCount = domainEventHandlers.Count,
            integrationEventHandlersCount = integrationEventHandlers.Count,
            commandHandlers,
            queryHandlers,
            domainEventHandlers,
            integrationEventHandlers
        };
    }
}
