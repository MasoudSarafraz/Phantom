using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Messaging.Abstractions;
using System.Collections.Concurrent;

namespace Phantom.Messaging.Kafka;

public class KafkaChannelAdapter : IChannelAdapter, IDisposable, IAsyncDisposable
{
    private readonly KafkaOptions _options;
    private readonly IMessageSerializer _serializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaChannelAdapter> _logger;

    private readonly ConcurrentDictionary<Type, List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>> _handlers = new();

    private IProducer<string, byte[]>? _producer;
    private IConsumer<string, byte[]>? _consumer;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;
    private readonly object _lock = new();
    private volatile bool _isStarted;
    private volatile bool _isDisposed;

    private readonly Acks _parsedAcks;
    private readonly SecurityProtocol _parsedSecurityProtocol;
    private readonly AutoOffsetReset _parsedAutoOffsetReset;
    private readonly SaslMechanism? _parsedSaslMechanism;

    public string ChannelName { get; }

    public bool IsStarted => _isStarted;

    public KafkaChannelAdapter(
        string channelName,
        KafkaOptions options,
        IMessageSerializer serializer,
        IServiceProvider serviceProvider,
        ILogger<KafkaChannelAdapter> logger)
    {
        ChannelName = channelName;
        _options = options;
        _serializer = serializer;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _parsedAcks = Enum.Parse<Acks>(options.Acks, ignoreCase: true);
        _parsedSecurityProtocol = Enum.Parse<SecurityProtocol>(options.SecurityProtocol, ignoreCase: true);
        _parsedAutoOffsetReset = Enum.Parse<AutoOffsetReset>(options.AutoOffsetReset, ignoreCase: true);
        if (!string.IsNullOrWhiteSpace(options.SaslMechanism))
            _parsedSaslMechanism = Enum.Parse<SaslMechanism>(options.SaslMechanism, ignoreCase: true);
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_isStarted) return Task.CompletedTask;

        lock (_lock)
        {
            if (_isStarted) return Task.CompletedTask;

            EnsureProducer();

            if (_handlers.Any())
            {
                StartConsumer();
            }

            _isStarted = true;
        }

        _logger.LogInformation("[Phantom] Kafka channel '{Channel}' started (BootstrapServers={Servers}, GroupId={GroupId})",
            ChannelName, _options.BootstrapServers, _options.GroupId);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _consumerCts?.Cancel();
        }

        if (_consumerTask != null)
        {
            try
            {
                await Task.WhenAny(_consumerTask, Task.Delay(TimeSpan.FromSeconds(10), ct));
            }
            catch (OperationCanceledException)
            {

            }
        }

        lock (_lock)
        {
            try
            {
                _consumer?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Phantom] Error closing Kafka consumer on channel '{Channel}'", ChannelName);
            }

            _consumer?.Dispose();
            _consumer = null;

            try
            {
                _producer?.Flush(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Phantom] Error flushing Kafka producer on channel '{Channel}'", ChannelName);
            }

            _producer?.Dispose();
            _producer = null;

            _consumerCts?.Dispose();
            _consumerCts = null;
            _consumerTask = null;
            _isStarted = false;
        }

        _logger.LogInformation("[Phantom] Kafka channel '{Channel}' stopped", ChannelName);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        EnsureProducer();

        var topic = GetTopicName(typeof(TEvent));
        var body = _serializer.Serialize(@event);
        var message = new Message<string, byte[]>
        {
            Key = @event.EventId.ToString(),
            Value = body,
            Headers = new Headers
            {
                { "EventType", System.Text.Encoding.UTF8.GetBytes(typeof(TEvent).AssemblyQualifiedName!) }
            }
        };

        try
        {
            var deliveryResult = await _producer!.ProduceAsync(topic, message, ct);

            _logger.LogInformation("[Phantom] Published {EventType} to channel '{Channel}' (Kafka topic='{Topic}', Offset={Offset})",
                typeof(TEvent).Name, ChannelName, topic, deliveryResult.Offset);
        }
        catch (ProduceException<string, byte[]> ex)
        {
            _logger.LogError(ex, "[Phantom] Failed to publish {EventType} to Kafka topic '{Topic}': {Error}",
                typeof(TEvent).Name, topic, ex.Error.Reason);
            throw;
        }
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
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

        if (_isStarted)
        {
            lock (_lock)
            {
                RestartConsumerIfNeeded();
            }
        }
    }

    private void EnsureProducer()
    {
        if (_producer != null) return;

        lock (_lock)
        {
            if (_producer != null) return;

            var config = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                Acks = _parsedAcks,
                MessageSendMaxRetries = _options.MessageRetries,
                RetryBackoffMs = (int)_options.RetryBackoff.TotalMilliseconds,
                BatchSize = _options.BatchSize,
                LingerMs = (int)_options.LingerMs.TotalMilliseconds,
                MessageMaxBytes = _options.MessageMaxBytes,
                SecurityProtocol = _parsedSecurityProtocol,
            };

            ApplySaslConfig(config);

            _producer = new ProducerBuilder<string, byte[]>(config).Build();
        }
    }

    private void StartConsumer()
    {
        if (_consumer != null) return;

        var topics = _handlers.Keys.Select(GetTopicName).ToList();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoCommitIntervalMs = (int)_options.AutoCommitInterval.TotalMilliseconds,
            AutoOffsetReset = _parsedAutoOffsetReset,
            EnableAutoCommit = _options.EnableAutoCommit,
            MaxPollIntervalMs = (int)_options.MaxPollInterval.TotalMilliseconds,
            SessionTimeoutMs = (int)_options.SessionTimeout.TotalMilliseconds,
            SecurityProtocol = _parsedSecurityProtocol,
        };

        ApplySaslConfig(config);

        _consumerCts = new CancellationTokenSource();
        _consumer = new ConsumerBuilder<string, byte[]>(config).Build();
        _consumer.Subscribe(topics);

        _consumerTask = Task.Run(() => ConsumeLoop(_consumerCts.Token), _consumerCts.Token);
    }

    private void RestartConsumerIfNeeded()
    {

        _consumerCts?.Cancel();
        try
        {
            _consumerTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch (AggregateException)
        {

        }
        catch (OperationCanceledException)
        {

        }

        try
        {
            _consumer?.Close();
        }
        catch {  }
        _consumer?.Dispose();

        _consumer = null;
        _consumerCts?.Dispose();
        _consumerCts = null;
        _consumerTask = null;

        if (_handlers.Any())
        {
            StartConsumer();
        }
    }

    private async Task ConsumeLoop(CancellationToken ct)
    {
        _logger.LogInformation("[Phantom] Kafka consumer started on channel '{Channel}' for topics: {Topics}",
            ChannelName, string.Join(", ", _handlers.Keys.Select(GetTopicName)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? consumeResult = null;
                try
                {
                    var consumer = _consumer;
                    if (consumer == null) break;

                    consumeResult = consumer.Consume(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "[Phantom] Kafka consume error on channel '{Channel}': {Error}",
                        ChannelName, ex.Error.Reason);

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                if (consumeResult == null) continue;

                try
                {
                    await ProcessMessage(consumeResult, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Phantom] Error processing Kafka message on channel '{Channel}'", ChannelName);

                    if (_options.EnableDeadLetterTopic)
                    {
                        await PublishToDeadLetterAsync(consumeResult, ex, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Phantom] Kafka consumer loop crashed on channel '{Channel}'", ChannelName);
        }
        finally
        {
            _logger.LogInformation("[Phantom] Kafka consumer stopped on channel '{Channel}'", ChannelName);
        }
    }

    private async Task ProcessMessage(ConsumeResult<string, byte[]> consumeResult, CancellationToken ct)
    {

        string? eventTypeName = null;
        if (consumeResult.Message.Headers != null)
        {
            var eventTypeHeader = consumeResult.Message.Headers.FirstOrDefault(h => h.Key == "EventType");
            if (eventTypeHeader != null)
            {
                eventTypeName = System.Text.Encoding.UTF8.GetString(eventTypeHeader.GetValueBytes());
            }
        }

        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            var topicSuffix = consumeResult.Topic;
            if (_options.TopicPrefix.Length > 0 && topicSuffix.StartsWith(_options.TopicPrefix + "."))
            {
                topicSuffix = topicSuffix[(_options.TopicPrefix.Length + 1)..];
            }

            var matchingType = _handlers.Keys.FirstOrDefault(t => t.Name == topicSuffix);
            if (matchingType != null)
            {
                eventTypeName = matchingType.AssemblyQualifiedName;
            }
        }

        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            _logger.LogWarning("[Phantom] Could not determine event type for Kafka message on topic '{Topic}'. Skipping.",
                consumeResult.Topic);
            return;
        }

        var eventType = Type.GetType(eventTypeName);
        if (eventType == null)
        {
            _logger.LogWarning("[Phantom] Could not resolve event type '{EventType}'. Skipping message.",
                eventTypeName);
            return;
        }

        if (!_handlers.TryGetValue(eventType, out var handlerInvokers) || handlerInvokers.Count == 0)
        {
            _logger.LogDebug("[Phantom] No handlers registered for event type {EventType}. Skipping.",
                eventType.Name);
            return;
        }

        var @event = _serializer.Deserialize(consumeResult.Message.Value, eventType) as IIntegrationEvent;
        if (@event == null)
        {
            _logger.LogWarning("[Phantom] Failed to deserialize Kafka message to {EventType}. Skipping.",
                eventType.Name);
            return;
        }

        foreach (var invoke in handlerInvokers)
        {
            using var scope = _serviceProvider.CreateScope();

            var idempotencyTracker = scope.ServiceProvider.GetService<IIdempotencyTracker>();
            if (idempotencyTracker is not null)
            {
                if (await idempotencyTracker.IsProcessedAsync(@event.EventId, ct))
                {
                    _logger.LogInformation("[Phantom] Skipping already processed event {EventType} with ID {EventId}",
                        eventType.Name, @event.EventId);
                    continue;
                }
            }

            await invoke(scope.ServiceProvider, @event, ct);

            if (idempotencyTracker is not null)
            {
                await idempotencyTracker.MarkAsProcessedAsync(@event.EventId, eventType.Name, ct);
            }
        }

        if (!_options.EnableAutoCommit)
        {
            try
            {
                var consumer = _consumer;
                consumer?.Commit(consumeResult);
            }
            catch (KafkaException ex)
            {
                _logger.LogWarning(ex, "[Phantom] Failed to commit Kafka offset on channel '{Channel}'", ChannelName);
            }
        }

        _logger.LogDebug("[Phantom] Processed {EventType} from Kafka topic '{Topic}' (Offset={Offset})",
            eventType.Name, consumeResult.Topic, consumeResult.Offset);
    }

    private async Task PublishToDeadLetterAsync(ConsumeResult<string, byte[]> consumeResult, Exception error, CancellationToken ct)
    {
        try
        {
            EnsureProducer();

            var deadLetterTopic = $"{_options.TopicPrefix}.dead-letter.{consumeResult.Topic}";
            var deadLetterMessage = new Message<string, byte[]>
            {
                Key = consumeResult.Message.Key,
                Value = consumeResult.Message.Value,
                Headers = new Headers()
            };

            if (consumeResult.Message.Headers != null)
            {
                foreach (var header in consumeResult.Message.Headers)
                {
                    deadLetterMessage.Headers.Add(header.Key, header.GetValueBytes());
                }
            }

            deadLetterMessage.Headers.Add("Error", System.Text.Encoding.UTF8.GetBytes(error.Message));
            deadLetterMessage.Headers.Add("ErrorType", System.Text.Encoding.UTF8.GetBytes(error.GetType().Name));
            deadLetterMessage.Headers.Add("OriginalTopic", System.Text.Encoding.UTF8.GetBytes(consumeResult.Topic));
            deadLetterMessage.Headers.Add("OriginalPartition", System.Text.Encoding.UTF8.GetBytes(consumeResult.Partition.Value.ToString()));
            deadLetterMessage.Headers.Add("OriginalOffset", System.Text.Encoding.UTF8.GetBytes(consumeResult.Offset.Value.ToString()));

            await _producer!.ProduceAsync(deadLetterTopic, deadLetterMessage, ct);

            _logger.LogWarning("[Phantom] Published failed message to dead-letter topic '{DeadLetterTopic}' (Original={Original})",
                deadLetterTopic, consumeResult.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Phantom] Failed to publish message to dead-letter topic on channel '{Channel}'", ChannelName);
        }
    }

    private string GetTopicName(Type eventType)
    {
        return $"{_options.TopicPrefix}.{eventType.Name}";
    }

    private void ApplySaslConfig(ClientConfig config)
    {
        if (_parsedSaslMechanism == null) return;

        config.SaslMechanism = _parsedSaslMechanism;
        if (!string.IsNullOrWhiteSpace(_options.SaslUsername))
            config.SaslUsername = _options.SaslUsername;
        if (!string.IsNullOrWhiteSpace(_options.SaslPassword))
            config.SaslPassword = _options.SaslPassword;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _consumerCts?.Cancel();
        try
        {
            _consumerTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch {  }

        try { _consumer?.Close(); } catch {  }
        _consumer?.Dispose();

        try { _producer?.Flush(TimeSpan.FromSeconds(10)); } catch {  }
        _producer?.Dispose();

        _consumerCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _consumerCts?.Cancel();

        if (_consumerTask != null)
        {
            try
            {
                await Task.WhenAny(_consumerTask, Task.Delay(TimeSpan.FromSeconds(10)));
            }
            catch {  }
        }

        try { _consumer?.Close(); } catch {  }
        _consumer?.Dispose();

        try { _producer?.Flush(TimeSpan.FromSeconds(10)); } catch {  }
        _producer?.Dispose();

        _consumerCts?.Dispose();
    }
}
