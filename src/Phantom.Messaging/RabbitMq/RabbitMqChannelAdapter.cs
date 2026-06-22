using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Messaging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;

namespace Phantom.Messaging.RabbitMq;

public class RabbitMqChannelAdapter : IChannelAdapter, IDisposable, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IMessageSerializer _serializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqChannelAdapter> _logger;
    private readonly ConcurrentDictionary<Type, List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>> _handlers = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private volatile bool _isStarted;
    private volatile bool _isDisposed;

    public string ChannelName { get; }

    public bool IsStarted
    {
        get
        {
            if (!_isStarted) return false;
            var connection = _connection;
            var channel = _channel;
            return connection is not null && connection.IsOpen && channel is not null && channel.IsOpen;
        }
    }

    public RabbitMqChannelAdapter(string channelName, RabbitMqOptions options, IMessageSerializer serializer, IServiceProvider serviceProvider, ILogger<RabbitMqChannelAdapter> logger)
    {
        ChannelName = channelName;
        _options = options;
        _serializer = serializer;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionAsync(ct);
        _isStarted = true;
        _logger.LogInformation("[Phantom] RabbitMQ channel '{Channel}' started", ChannelName);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _connectionLock.WaitAsync(ct);
        try
        {
            await CloseInnerAsync(ct);
            _isStarted = false;
        }
        finally
        {
            _connectionLock.Release();
        }
        _logger.LogInformation("[Phantom] RabbitMQ channel '{Channel}' stopped", ChannelName);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested) return;

        await EnsureConnectionAsync(ct);
        var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not available after EnsureConnectionAsync.");

        var body = _serializer.Serialize(@event);
        var properties = new BasicProperties
        {
            Persistent = _options.Durable,
            Type = typeof(TEvent).AssemblyQualifiedName,
            DeliveryMode = _options.Durable ? DeliveryModes.Persistent : DeliveryModes.Transient
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: typeof(TEvent).Name,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        _logger.LogInformation("[Phantom] Published {EventType} to channel '{Channel}' (RabbitMQ)", typeof(TEvent).Name, ChannelName);
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

        if (_isStarted && _channel is { IsOpen: true })
        {
            Task.Run(async () =>
            {
                await _connectionLock.WaitAsync();
                try
                {
                    if (_channel is { IsOpen: true })
                    {
                        await SetupConsumerAsync(typeof(TEvent), CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Phantom] Failed to register late subscriber for event {EventType} on channel '{Channel}'", typeof(TEvent).Name, ChannelName);
                }
                finally
                {
                    _connectionLock.Release();
                }
            }).ObserveAsException(_logger, ChannelName);
        }
    }

    private async Task EnsureConnectionAsync(CancellationToken ct)
    {
        var existingConnection = _connection;
        var existingChannel = _channel;
        if (existingConnection is { IsOpen: true } && existingChannel is { IsOpen: true }) return;

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true } && _channel is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                ConsumerDispatchConcurrency = 1
            };

            if (_options.SslOptions is not null)
            {
                factory.Ssl = _options.SslOptions;
            }
            if (!string.IsNullOrWhiteSpace(_options.ClientProvidedName))
            {
                factory.ClientProvidedName = _options.ClientProvidedName;
            }
            factory.RequestedHeartbeat = _options.RequestedHeartbeat;

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(options: null, cancellationToken: ct);

            await _channel.ExchangeDeclareAsync(
                exchange: _options.Exchange,
                type: ExchangeType.Topic,
                durable: _options.Durable,
                autoDelete: _options.AutoDelete,
                passive: false,
                noWait: false,
                arguments: null,
                cancellationToken: ct);

            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _options.PrefetchCount,
                global: false,
                cancellationToken: ct);

            foreach (var (eventType, _) in _handlers)
            {
                await SetupConsumerAsync(eventType, ct);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task SetupConsumerAsync(Type eventType, CancellationToken ct)
    {
        var channel = _channel;
        if (channel is null) return;

        var queueName = $"{_options.ConsumerGroup}.{eventType.Name}";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Durable,
            exclusive: false,
            autoDelete: false,
            passive: false,
            noWait: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.Exchange,
            routingKey: eventType.Name,
            arguments: null,
            noWait: false,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var deliveryChannel = sender as IChannel ?? channel;
            try
            {
                IIntegrationEvent? @event = null;
                try
                {
                    @event = _serializer.Deserialize<IIntegrationEvent>(ea.Body.ToArray());
                }
                catch (Exception serializeEx)
                {
                    _logger.LogError(serializeEx, "[Phantom] Failed to deserialize message on channel '{Channel}' (routingKey={RoutingKey})", ChannelName, ea.RoutingKey);
                    await deliveryChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ea.CancellationToken);
                    return;
                }

                if (@event is null)
                {
                    _logger.LogWarning("[Phantom] Deserialized event was null on channel '{Channel}' (routingKey={RoutingKey})", ChannelName, ea.RoutingKey);
                    await deliveryChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ea.CancellationToken);
                    return;
                }

                if (_handlers.TryGetValue(eventType, out var handlerInvokers))
                {
                    foreach (var invoke in handlerInvokers)
                    {
                        using var scope = _serviceProvider.CreateScope();

                        var idempotencyTracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();
                        if (idempotencyTracker is not null)
                        {
                            if (await idempotencyTracker.IsProcessedAsync(@event.EventId, ea.CancellationToken))
                            {
                                _logger.LogInformation("[Phantom] Skipping already processed event {EventType} with ID {EventId}",
                                    eventType.Name, @event.EventId);
                                continue;
                            }
                        }

                        await invoke(scope.ServiceProvider, @event, ea.CancellationToken);

                        if (idempotencyTracker is not null)
                        {
                            await idempotencyTracker.MarkAsProcessedAsync(@event.EventId, eventType.Name, ea.CancellationToken);
                        }
                    }
                }

                await deliveryChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ea.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Error processing message on channel {Channel}", ChannelName);
                try
                {
                    await deliveryChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ea.CancellationToken);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "[Phantom] Failed to nack message on channel '{Channel}'", ChannelName);
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer,
            cancellationToken: ct);
    }

    private async Task CloseInnerAsync(CancellationToken ct)
    {
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;

        if (channel is not null)
        {
            try
            {
                if (channel.IsOpen)
                {
                    await channel.CloseAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Phantom] Error closing RabbitMQ channel on channel '{Channel}'", ChannelName);
            }
            try { await channel.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Phantom] Error disposing RabbitMQ channel on channel '{Channel}'", ChannelName); }
        }

        if (connection is not null)
        {
            try
            {
                if (connection.IsOpen)
                {
                    await connection.CloseAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Phantom] Error closing RabbitMQ connection on channel '{Channel}'", ChannelName);
            }
            try { await connection.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Phantom] Error disposing RabbitMQ connection on channel '{Channel}'", ChannelName); }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(RabbitMqChannelAdapter));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isStarted = false;

        await _connectionLock.WaitAsync();
        try
        {
            await CloseInnerAsync(CancellationToken.None);
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isStarted = false;

        if (_connectionLock.Wait(0))
        {
            try
            {
                CloseInnerAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Phantom] Synchronous dispose encountered an error on channel '{Channel}'", ChannelName);
            }
            finally
            {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
        }
        else
        {
            _logger.LogWarning("[Phantom] Dispose called while connection lock is held on channel '{Channel}'. Resources will be released when async path completes.", ChannelName);
        }
    }
}

internal static class TaskExceptionExtensions
{
    public static void ObserveAsException(this Task task, ILogger logger, string channelName)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
            {
                logger.LogError(t.Exception, "[Phantom] Background task failed on channel '{Channel}'", channelName);
            }
        }, TaskScheduler.Default);
    }
}
