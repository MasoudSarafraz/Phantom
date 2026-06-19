using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Messaging.Abstractions;
using System.Collections.Concurrent;

namespace Phantom.Messaging.InMemory;

public class InMemoryChannelAdapter : IChannelAdapter, IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryChannelAdapter> _logger;
    private readonly ConcurrentDictionary<Type, List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>> _handlers = new();
    private volatile bool _isStarted;

    public string ChannelName { get; }

    public bool IsStarted => _isStarted;

    public InMemoryChannelAdapter(string channelName, IServiceProvider serviceProvider, ILogger<InMemoryChannelAdapter> logger)
    { ChannelName = channelName; _serviceProvider = serviceProvider; _logger = logger; }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlerInvokers)) return Task.CompletedTask;
        var tasks = handlerInvokers.Select(async invoke =>
        {
            using var scope = _serviceProvider.CreateScope();

            var idempotencyTracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();
            if (idempotencyTracker is not null)
            {
                if (await idempotencyTracker.IsProcessedAsync(@event.EventId, ct))
                {
                    _logger.LogInformation("[Phantom] Skipping already processed event {EventType} with ID {EventId}",
                        typeof(TEvent).Name, @event.EventId);
                    return;
                }
            }

            await invoke(scope.ServiceProvider, @event, ct);

            if (idempotencyTracker is not null)
            {
                await idempotencyTracker.MarkAsProcessedAsync(@event.EventId, typeof(TEvent).Name, ct);
            }
        });
        return Task.WhenAll(tasks);
    }

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>
    {
        var invokers = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>());
        lock (invokers)
        {
            invokers.Add((sp, evt, ct) =>
            {
                var handler = sp.GetRequiredService<IIntegrationEventHandler<TEvent>>();
                return handler.HandleAsync((TEvent)evt, ct);
            });
        }
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _isStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _isStarted = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No unmanaged resources to dispose for InMemory adapter.
        // Implemented for interface uniformity with RabbitMQ and Kafka adapters.
    }

    public ValueTask DisposeAsync()
    {
        // No unmanaged resources to dispose for InMemory adapter.
        return ValueTask.CompletedTask;
    }
}
