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
    private volatile bool _isDisposed;

    public string ChannelName { get; }

    public bool IsStarted => _isStarted;

    public InMemoryChannelAdapter(string channelName, IServiceProvider serviceProvider, ILogger<InMemoryChannelAdapter> logger)
    {
        ChannelName = channelName;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return;

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlerInvokers) || handlerInvokers.Count == 0)
        {
            return;
        }

        var tasks = handlerInvokers.Select(async invoke =>
        {
            using var scope = _serviceProvider.CreateScope();
            IIdempotencyTracker? idempotencyTracker = null;

            try
            {
                idempotencyTracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();
                if (idempotencyTracker is not null)
                {
                    if (await idempotencyTracker.IsProcessedAsync(@event.EventId, ct))
                    {
                        _logger.LogInformation("[Phantom] Skipping already processed event {EventType} with ID {EventId}",
                            typeof(TEvent).Name, @event.EventId);
                        return;
                    }
                }
            }
            catch (Exception idemEx)
            {
                _logger.LogWarning(idemEx,
                    "[Phantom] Idempotency check failed for event {EventType} with ID {EventId}. Continuing with handler invocation.",
                    typeof(TEvent).Name, @event.EventId);
            }

            try
            {
                await invoke(scope.ServiceProvider, @event, ct);
            }
            catch (Exception handlerEx)
            {
                _logger.LogError(handlerEx,
                    "[Phantom] Handler failed for event {EventType} with ID {EventId} on channel '{Channel}'.",
                    typeof(TEvent).Name, @event.EventId, ChannelName);
                return;
            }

            if (idempotencyTracker is not null)
            {
                try
                {
                    await idempotencyTracker.MarkAsProcessedAsync(@event.EventId, typeof(TEvent).Name, ct);
                }
                catch (Exception markEx)
                {
                    _logger.LogWarning(markEx,
                        "[Phantom] Failed to mark event {EventType} with ID {EventId} as processed. Redelivery may cause a duplicate handler invocation.",
                        typeof(TEvent).Name, @event.EventId);
                }
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Phantom] Unexpected error during publish on channel '{Channel}'.",
                ChannelName);
        }
    }

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>
    {
        ThrowIfDisposed();
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
        ThrowIfDisposed();
        _isStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _isStarted = false;
        return Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(InMemoryChannelAdapter));
    }

    public void Dispose()
    {
        _isDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        _isDisposed = true;
        return ValueTask.CompletedTask;
    }
}
