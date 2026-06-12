using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
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
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _connectionLock = new();
    private bool _isStarted;

    public string ChannelName { get; }

    public bool IsStarted
    {
        get
        {
            lock (_connectionLock)
            {
                return _isStarted && _connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen;
            }
        }
    }

    public RabbitMqChannelAdapter(string channelName, RabbitMqOptions options, IMessageSerializer serializer, IServiceProvider serviceProvider, ILogger<RabbitMqChannelAdapter> logger)
    { ChannelName = channelName; _options = options; _serializer = serializer; _serviceProvider = serviceProvider; _logger = logger; }

    public Task StartAsync(CancellationToken ct = default)
    {
        EnsureConnection();
        _isStarted = true;
        _logger.LogInformation("[Phantom] RabbitMQ channel '{Channel}' started", ChannelName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_connectionLock)
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            _channel = null;
            _connection = null;
            _isStarted = false;
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        EnsureConnection();
        var body = _serializer.Serialize(@event);
        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = _options.Durable;
        properties.Type = typeof(TEvent).AssemblyQualifiedName;
        properties.DeliveryMode = 2;
        _channel.BasicPublish(exchange: _options.Exchange, routingKey: typeof(TEvent).Name, mandatory: false, basicProperties: properties, body: body);
        _logger.LogInformation("[Phantom] Published {EventType} to channel '{Channel}' (RabbitMQ)", typeof(TEvent).Name, ChannelName);
        return Task.CompletedTask;
    }

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent>
    {
        var subscriptions = _subscriptions.GetOrAdd(typeof(TEvent), _ => new List<Subscription>());
        lock (subscriptions) { subscriptions.Add(new Subscription(typeof(THandler))); }
    }

    private void EnsureConnection()
    {
        if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen) return;
        lock (_connectionLock)
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen) return;
            var factory = new ConnectionFactory
            {
                HostName = _options.Host, Port = _options.Port, UserName = _options.Username, Password = _options.Password,
                VirtualHost = _options.VirtualHost, DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true, NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: _options.Exchange, type: ExchangeType.Topic, durable: _options.Durable, autoDelete: _options.AutoDelete);
            _channel.BasicQos(0, _options.PrefetchCount, false);
            foreach (var (eventType, subs) in _subscriptions) SetupConsumer(eventType, subs);
        }
    }

    private void SetupConsumer(Type eventType, List<Subscription> subs)
    {
        var queueName = $"{_options.ConsumerGroup}.{eventType.Name}";
        _channel!.QueueDeclare(queue: queueName, durable: _options.Durable, exclusive: false, autoDelete: false);
        _channel.QueueBind(queue: queueName, exchange: _options.Exchange, routingKey: eventType.Name);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var @event = _serializer.Deserialize<IIntegrationEvent>(ea.Body.ToArray());
                foreach (var sub in subs)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService(sub.HandlerType);
                    var handleMethod = sub.HandlerType.GetMethod("HandleAsync")!;
                    await (Task)handleMethod.Invoke(handler, new object[] { @event, CancellationToken.None })!;
                }
                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Phantom] Error processing message on channel {Channel}", ChannelName); _channel!.BasicNack(ea.DeliveryTag, false, true); }
        };
        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
    }

    public void Dispose() { _channel?.Dispose(); _connection?.Dispose(); }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    private record Subscription(Type HandlerType);
}
