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
using System.Text;

namespace Phantom.Messaging.RabbitMq;

public class RabbitMqChannelAdapter : IChannelAdapter, IDisposable, IAsyncDisposable
{
    private const string EventTypeHeader = "Phantom-EventType";

    private readonly RabbitMqOptions _options;
    private readonly IMessageSerializer _serializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqChannelAdapter> _logger;
    private readonly ConcurrentDictionary<Type, List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>> _handlers = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly CancellationTokenSource _disposedCts = new();
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
            await CloseInnerAsync(CancellationToken.None);
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

        var eventType = typeof(TEvent);
        var body = _serializer.Serialize(@event);
        var properties = new BasicProperties
        {
            Persistent = _options.Durable,
            Type = eventType.AssemblyQualifiedName,
            DeliveryMode = _options.Durable ? DeliveryModes.Persistent : DeliveryModes.Transient,
            MessageId = @event.EventId.ToString(),
            CorrelationId = @event.CorrelationId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [EventTypeHeader] = Encoding.UTF8.GetBytes(eventType.AssemblyQualifiedName!)
            }
        };

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(RabbitMqChannelAdapter));
            if (ct.IsCancellationRequested) return;

            IChannel? channelSnapshot;
            try
            {
                await EnsureConnectionAsync(ct);
                channelSnapshot = _channel;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            if (channelSnapshot is null || !channelSnapshot.IsOpen)
            {
                if (attempt < maxAttempts)
                {
                    _logger.LogWarning("[Phantom] RabbitMQ channel not open on attempt {Attempt}/{Max} for publish on '{Channel}'. Retrying in {Delay}ms.",
                        attempt, maxAttempts, ChannelName, 200);
                    try { await Task.Delay(200, ct); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }
                throw new InvalidOperationException($"RabbitMQ channel '{ChannelName}' is not available after {maxAttempts} attempts.");
            }

            try
            {
                await channelSnapshot.BasicPublishAsync(
                    exchange: _options.Exchange,
                    routingKey: eventType.Name,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: ct);

                _logger.LogInformation("[Phantom] Published {EventType} to channel '{Channel}' (RabbitMQ)", eventType.Name, ChannelName);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt < maxAttempts && IsTransient(ex))
                {
                    _logger.LogWarning(ex, "[Phantom] Transient publish failure attempt {Attempt}/{Max} on channel '{Channel}'. Will retry.",
                        attempt, maxAttempts, ChannelName);
                    try { await Task.Delay(200 * attempt, ct); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }
                _logger.LogError(ex, "[Phantom] Failed to publish {EventType} to channel '{Channel}' after {Attempts} attempts.",
                    eventType.Name, ChannelName, attempt);
                throw;
            }
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

        if (_isStarted && _channel is { IsOpen: true })
        {
            RegisterLateSubscriberAsync(typeof(TEvent)).ObserveAsException(_logger, ChannelName);
        }
    }

    private async Task RegisterLateSubscriberAsync(Type eventType)
    {
        var maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (_isDisposed) return;
            if (_disposedCts.IsCancellationRequested) return;

            try
            {
                await _connectionLock.WaitAsync(_disposedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                if (_isDisposed) return;
                if (_channel is { IsOpen: true })
                {
                    await SetupConsumerAsync(eventType, _disposedCts.Token);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Failed to register late subscriber for event {EventType} on channel '{Channel}' (attempt {Attempt}/{Max}).",
                    eventType.Name, ChannelName, attempt, maxAttempts);
            }
            finally
            {
                _connectionLock.Release();
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), _disposedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogError("[Phantom] Giving up on late subscriber for event {EventType} on channel '{Channel}' after {Attempts} attempts.",
            eventType.Name, ChannelName, maxAttempts);
    }

    private async Task EnsureConnectionAsync(CancellationToken ct)
    {
        var existingConnection = _connection;
        var existingChannel = _channel;
        if (existingConnection is { IsOpen: true } && existingChannel is { IsOpen: true }) return;

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(RabbitMqChannelAdapter));
            if (_connection is { IsOpen: true } && _channel is { IsOpen: true }) return;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
                TopologyRecoveryEnabled = _options.TopologyRecoveryEnabled,
                NetworkRecoveryInterval = _options.NetworkRecoveryInterval,
                RequestedConnectionTimeout = _options.RequestedConnectionTimeout,
                SocketReadTimeout = _options.SocketReadTimeout,
                SocketWriteTimeout = _options.SocketWriteTimeout,
                ContinuationTimeout = _options.ContinuationTimeout,
                RequestedHeartbeat = _options.RequestedHeartbeat,
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

            _connection = await factory.CreateConnectionAsync(ct);
            _connection.RecoverySucceededAsync += (_, _) =>
            {
                _logger.LogInformation("[Phantom] RabbitMQ connection recovered on channel '{Channel}'", ChannelName);
                return Task.CompletedTask;
            };
            _connection.ConnectionShutdownAsync += (_, args) =>
            {
                _logger.LogWarning("[Phantom] RabbitMQ connection shutdown on channel '{Channel}': {Reason} (replyCode={ReplyCode})",
                    ChannelName, args.ReplyText, args.ReplyCode);
                return Task.CompletedTask;
            };
            _connection.CallbackExceptionAsync += (_, args) =>
            {
                _logger.LogError(args.Exception, "[Phantom] RabbitMQ connection callback exception on channel '{Channel}'", ChannelName);
                return Task.CompletedTask;
            };

            _channel = await _connection.CreateChannelAsync(options: null, cancellationToken: ct);
            _channel.CallbackExceptionAsync += (_, args) =>
            {
                _logger.LogError(args.Exception, "[Phantom] RabbitMQ channel callback exception on channel '{Channel}'", ChannelName);
                return Task.CompletedTask;
            };
            _channel.ChannelShutdownAsync += (_, args) =>
            {
                _logger.LogWarning("[Phantom] RabbitMQ channel shutdown on channel '{Channel}': {Reason} (replyCode={ReplyCode})",
                    ChannelName, args.ReplyText, args.ReplyCode);
                return Task.CompletedTask;
            };

            await _channel.ExchangeDeclareAsync(
                exchange: _options.Exchange,
                type: ExchangeType.Topic,
                durable: _options.Durable,
                autoDelete: _options.AutoDelete,
                passive: false,
                noWait: false,
                arguments: null,
                cancellationToken: ct);

            if (!string.IsNullOrWhiteSpace(_options.DeadLetterExchange))
            {
                await _channel.ExchangeDeclareAsync(
                    exchange: _options.DeadLetterExchange,
                    type: ExchangeType.Direct,
                    durable: _options.Durable,
                    autoDelete: false,
                    passive: false,
                    noWait: false,
                    arguments: null,
                    cancellationToken: ct);
            }

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
        var queueArguments = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(_options.DeadLetterExchange))
        {
            queueArguments["x-dead-letter-exchange"] = _options.DeadLetterExchange;
        }
        if (_options.MessageTtl > TimeSpan.Zero)
        {
            queueArguments["x-message-ttl"] = (int)_options.MessageTtl.TotalMilliseconds;
        }

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: _options.Durable,
            exclusive: false,
            autoDelete: false,
            passive: false,
            noWait: false,
            arguments: queueArguments.Count > 0 ? queueArguments : null,
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
                var resolvedType = ResolveEventType(ea, eventType);
                if (resolvedType is null)
                {
                    _logger.LogError("[Phantom] Could not resolve event type for message on channel '{Channel}' (routingKey={RoutingKey}, typeHeader={TypeHeader}). Sending to DLX if configured.",
                        ChannelName, ea.RoutingKey, ea.BasicProperties?.Type);
                    await SafeNackAsync(deliveryChannel, ea.DeliveryTag, requeue: false, ea.CancellationToken);
                    return;
                }

                IIntegrationEvent? @event = null;
                try
                {
                    @event = _serializer.Deserialize(ea.Body.ToArray(), resolvedType) as IIntegrationEvent;
                }
                catch (Exception serializeEx)
                {
                    _logger.LogError(serializeEx, "[Phantom] Failed to deserialize message on channel '{Channel}' (routingKey={RoutingKey}, resolvedType={ResolvedType}). Sending to DLX if configured.",
                        ChannelName, ea.RoutingKey, resolvedType.Name);
                    await SafeNackAsync(deliveryChannel, ea.DeliveryTag, requeue: false, ea.CancellationToken);
                    return;
                }

                if (@event is null)
                {
                    _logger.LogWarning("[Phantom] Deserialized event was null on channel '{Channel}' (routingKey={RoutingKey}). Sending to DLX if configured.",
                        ChannelName, ea.RoutingKey);
                    await SafeNackAsync(deliveryChannel, ea.DeliveryTag, requeue: false, ea.CancellationToken);
                    return;
                }

                if (_handlers.TryGetValue(eventType, out var handlerInvokers))
                {
                    var handlerFailure = false;
                    foreach (var invoke in handlerInvokers)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        IIdempotencyTracker? idempotencyTracker = null;
                        var alreadyProcessed = false;

                        try
                        {
                            idempotencyTracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();
                            if (idempotencyTracker is not null)
                            {
                                if (await idempotencyTracker.IsProcessedAsync(@event.EventId, ea.CancellationToken))
                                {
                                    _logger.LogInformation("[Phantom] Skipping already processed event {EventType} with ID {EventId}",
                                        eventType.Name, @event.EventId);
                                    alreadyProcessed = true;
                                }
                            }
                        }
                        catch (Exception idemEx)
                        {
                            _logger.LogWarning(idemEx, "[Phantom] Idempotency check failed for event {EventType} with ID {EventId}. Continuing with handler invocation.",
                                eventType.Name, @event.EventId);
                        }

                        if (alreadyProcessed) continue;

                        try
                        {
                            await invoke(scope.ServiceProvider, @event, ea.CancellationToken);
                        }
                        catch (Exception handlerEx)
                        {
                            handlerFailure = true;
                            _logger.LogError(handlerEx, "[Phantom] Handler failed for event {EventType} with ID {EventId} on channel '{Channel}'.",
                                eventType.Name, @event.EventId, ChannelName);
                            continue;
                        }

                        if (idempotencyTracker is not null)
                        {
                            try
                            {
                                await idempotencyTracker.MarkAsProcessedAsync(@event.EventId, eventType.Name, ea.CancellationToken);
                            }
                            catch (Exception markEx)
                            {
                                _logger.LogWarning(markEx, "[Phantom] Failed to mark event {EventType} with ID {EventId} as processed. Possible duplicate on redelivery.",
                                    eventType.Name, @event.EventId);
                            }
                        }
                    }

                    if (handlerFailure)
                    {
                        _logger.LogWarning("[Phantom] One or more handlers failed for event {EventType} with ID {EventId} on channel '{Channel}'. Nacking with requeue.",
                            eventType.Name, @event.EventId, ChannelName);
                        await SafeNackAsync(deliveryChannel, ea.DeliveryTag, requeue: true, ea.CancellationToken);
                        return;
                    }
                }

                try
                {
                    await deliveryChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ea.CancellationToken);
                }
                catch (Exception ackEx)
                {
                    _logger.LogWarning(ackEx, "[Phantom] Failed to ack message on channel '{Channel}'.", ChannelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Unexpected error processing message on channel {Channel}", ChannelName);
                await SafeNackAsync(deliveryChannel, ea.DeliveryTag, requeue: true, ea.CancellationToken);
            }
        };

        consumer.ShutdownAsync += (_, args) =>
        {
            _logger.LogWarning("[Phantom] Consumer shutdown on channel '{Channel}' for queue '{Queue}': {Reason} (replyCode={ReplyCode})",
                ChannelName, queueName, args.ReplyText, args.ReplyCode);
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) =>
        {
            _logger.LogDebug("[Phantom] Consumer unregistered on channel '{Channel}' for queue '{Queue}'", ChannelName, queueName);
            return Task.CompletedTask;
        };
        consumer.RegisteredAsync += (_, _) =>
        {
            _logger.LogDebug("[Phantom] Consumer registered on channel '{Channel}' for queue '{Queue}'", ChannelName, queueName);
            return Task.CompletedTask;
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

    private Type? ResolveEventType(BasicDeliverEventArgs ea, Type fallbackType)
    {
        var typeHeader = ea.BasicProperties?.Type;
        if (!string.IsNullOrWhiteSpace(typeHeader))
        {
            var resolved = Type.GetType(typeHeader);
            if (resolved is not null) return resolved;

            var typeNameOnly = typeHeader.Split(',')[0].Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = assembly.GetType(typeNameOnly);
                    if (t is not null) return t;
                }
                catch { }
            }
        }

        if (ea.BasicProperties?.Headers is not null
            && ea.BasicProperties.Headers.TryGetValue(EventTypeHeader, out var headerValue))
        {
            var headerStr = headerValue switch
            {
                byte[] b => Encoding.UTF8.GetString(b),
                string s => s,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(headerStr))
            {
                var resolved = Type.GetType(headerStr);
                if (resolved is not null) return resolved;

                var typeNameOnly = headerStr.Split(',')[0].Trim();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = assembly.GetType(typeNameOnly);
                        if (t is not null) return t;
                    }
                    catch { }
                }
            }
        }

        return fallbackType;
    }

    private async Task SafeNackAsync(IChannel channel, ulong deliveryTag, bool requeue, CancellationToken ct)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue, cancellationToken: ct);
        }
        catch (Exception nackEx)
        {
            _logger.LogError(nackEx, "[Phantom] Failed to nack message (deliveryTag={DeliveryTag}) on channel '{Channel}'.", deliveryTag, ChannelName);
        }
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is RabbitMQ.Client.Exceptions.AlreadyClosedException) return true;
        if (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException) return true;
        if (ex is RabbitMQ.Client.Exceptions.OperationInterruptedException) return false;
        if (ex is System.Net.Sockets.SocketException) return true;
        if (ex is TimeoutException) return true;
        if (ex.InnerException is not null) return IsTransient(ex.InnerException);
        return false;
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

        try { _disposedCts.Cancel(); }
        catch (Exception ex) { _logger.LogWarning(ex, "[Phantom] Error cancelling disposed CTS on channel '{Channel}'", ChannelName); }

        await _connectionLock.WaitAsync();
        try
        {
            await CloseInnerAsync(CancellationToken.None);
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
            _disposedCts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _isStarted = false;

        try { _disposedCts.Cancel(); }
        catch (Exception ex) { _logger.LogWarning(ex, "[Phantom] Error cancelling disposed CTS on channel '{Channel}'", ChannelName); }

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
                _disposedCts.Dispose();
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
