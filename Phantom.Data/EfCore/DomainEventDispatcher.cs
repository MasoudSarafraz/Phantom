using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Data.EfCore;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger) { _serviceProvider = serviceProvider; _logger = logger; }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);
        foreach (var handler in handlers)
        {
            var handleMethod = handlerType.GetMethod("HandleAsync")!;
            await (Task)handleMethod.Invoke(handler, new object[] { domainEvent, ct })!;
            _logger.LogInformation("[Phantom] Domain event {EventType} handled by {HandlerType}", eventType.Name, handler!.GetType().Name);
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    { foreach (var domainEvent in domainEvents) await DispatchAsync(domainEvent, ct); }
}
