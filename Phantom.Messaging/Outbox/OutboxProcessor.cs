using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.Outbox;

public class OutboxProcessor : BackgroundService, IOutboxProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _batchSize = 100;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger) { _serviceProvider = serviceProvider; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Phantom] Outbox Processor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "[Phantom] Outbox Processor error"); }
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await repository.GetPendingAsync(_batchSize, ct);
        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType == null) { await repository.MarkAsFailedAsync(message.Id, $"Cannot resolve type: {message.EventType}", ct); continue; }
                var @event = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType) as IIntegrationEvent;
                if (@event == null) { await repository.MarkAsFailedAsync(message.Id, "Deserialization returned null", ct); continue; }
                if (message.Channel != "default") await publisher.PublishAsync(@event, message.Channel, ct);
                else await publisher.PublishAsync(@event, ct);
                await repository.MarkAsPublishedAsync(message.Id, ct);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Phantom] Failed to publish outbox message {MessageId}", message.Id); await repository.MarkAsFailedAsync(message.Id, ex.Message, ct); }
        }
    }
}
