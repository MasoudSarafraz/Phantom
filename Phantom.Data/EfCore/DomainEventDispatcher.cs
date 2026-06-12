using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;

namespace Phantom.Data.EfCore;

/// <summary>
/// Dispatches domain events to registered <see cref="IDomainEventHandler{TEvent}"/> handlers.
/// Uses cached reflection for performance and provides per-handler error handling
/// to prevent one failed handler from blocking the remaining handlers.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _handleMethodCache = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler == null) continue;

            try
            {
                var handleMethod = _handleMethodCache.GetOrAdd(handlerType,
                    t => t.GetMethod("HandleAsync") ?? throw new InvalidOperationException(
                        $"Type {t.Name} does not implement HandleAsync method."));

                await (Task)handleMethod.Invoke(handler, new object[] { domainEvent, ct })!;

                _logger.LogInformation("[Phantom] Domain event {EventType} handled by {HandlerType}",
                    eventType.Name, handler.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Phantom] Failed to handle domain event {EventType} in handler {HandlerType}. Continuing with remaining handlers.",
                    eventType.Name, handler.GetType().Name);
            }
        }
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, ct);
        }
    }
}
