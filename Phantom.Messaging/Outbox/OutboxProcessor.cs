using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Outbox;

namespace Phantom.Messaging.Outbox;

public class OutboxProcessor : BackgroundService, IOutboxProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        IMessageSerializer serializer,
        ILogger<OutboxProcessor> logger,
        int batchSize = 100,
        TimeSpan? pollingInterval = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batchSize = batchSize > 0 ? batchSize : 100;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Phantom] Outbox Processor started (batch size: {BatchSize}, polling interval: {PollingInterval}s)",
            _batchSize, _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Outbox Processor error");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _logger.LogInformation("[Phantom] Outbox Processor stopped");
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await repository.GetPendingAsync(_batchSize, ct);

        foreach (var message in messages)
        {
            if (ct.IsCancellationRequested) break;

            await ProcessMessageAsync(message, repository, publisher, ct);
        }
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IOutboxMessageRepository repository,
        IEventPublisher publisher,
        CancellationToken ct)
    {
        try
        {
            var eventType = ResolveEventType(message.EventType);
            if (eventType is null)
            {
                await repository.MarkAsFailedAsync(message.Id, $"Cannot resolve type: {message.EventType}", ct);
                return;
            }

            var data = System.Text.Encoding.UTF8.GetBytes(message.Payload);
            var @event = _serializer.Deserialize(data, eventType) as IIntegrationEvent;
            if (@event is null)
            {
                await repository.MarkAsFailedAsync(message.Id, $"Deserialization returned null for type: {message.EventType}", ct);
                return;
            }

            if (message.Channel != OutboxMessage.DefaultChannel)
            {
                await publisher.PublishAsync(@event, message.Channel, ct);
            }
            else
            {
                await publisher.PublishAsync(@event, ct);
            }

            await repository.MarkAsPublishedAsync(message.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Phantom] Failed to publish outbox message {MessageId}", message.Id);

            try
            {
                await repository.IncrementRetryCountAsync(message.Id, ex.Message, ct);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "[Phantom] Failed to increment retry count for outbox message {MessageId}", message.Id);
            }
        }
    }

    private static Type? ResolveEventType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        var eventType = Type.GetType(typeName);
        if (eventType is not null) return eventType;

        var typeNameWithoutAssembly = typeName.Split(',')[0].Trim();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                eventType = assembly.GetType(typeNameWithoutAssembly);
                if (eventType is not null) return eventType;
            }
            catch
            {
            }
        }

        return null;
    }
}
