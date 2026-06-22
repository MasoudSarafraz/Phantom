using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.Outbox;

public class OutboxProcessor : BackgroundService, IOutboxProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageSerializer _serializer;
    private readonly IResiliencePipeline _resilience;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;
    private static readonly TimeSpan ProcessingLockDuration = TimeSpan.FromMinutes(5);

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        IMessageSerializer serializer,
        IResiliencePipeline resilience,
        ILogger<OutboxProcessor> logger,
        int batchSize = 100,
        TimeSpan? pollingInterval = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Phantom] Outbox Processor error");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
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
        var lockedUntil = DateTimeOffset.UtcNow.Add(ProcessingLockDuration);
        bool lockAcquired;

        try
        {
            lockAcquired = await repository.TryMarkAsProcessingAsync(message.Id, lockedUntil, ct);
        }
        catch (Exception lockEx)
        {
            _logger.LogWarning(lockEx, "[Phantom] Failed to acquire processing lock for outbox message {MessageId}", message.Id);
            return;
        }

        if (!lockAcquired)
        {
            _logger.LogDebug("[Phantom] Outbox message {MessageId} is already being processed by another worker. Skipping.", message.Id);
            return;
        }

        try
        {
            var eventType = ResolveEventType(message.EventType);
            if (eventType is null)
            {
                await SafeMarkTerminalFailureAsync(repository, message.Id,
                    $"Cannot resolve type: {message.EventType}", ct);
                return;
            }

            IIntegrationEvent? @event;
            try
            {
                var data = System.Text.Encoding.UTF8.GetBytes(message.Payload);
                @event = _serializer.Deserialize(data, eventType) as IIntegrationEvent;
            }
            catch (Exception deserialEx)
            {
                _logger.LogError(deserialEx, "[Phantom] Failed to deserialize outbox message {MessageId} (type={EventType})",
                    message.Id, message.EventType);
                await SafeMarkTerminalFailureAsync(repository, message.Id,
                    $"Deserialization failed: {deserialEx.Message}", ct);
                return;
            }

            if (@event is null)
            {
                await SafeMarkTerminalFailureAsync(repository, message.Id,
                    $"Deserialization returned null for type: {message.EventType}", ct);
                return;
            }

            try
            {
                await _resilience.ExecuteAsync(async token =>
                {
                    if (message.Channel != OutboxMessage.DefaultChannel)
                    {
                        await publisher.PublishAsync(@event, message.Channel, token);
                    }
                    else
                    {
                        await publisher.PublishAsync(@event, token);
                    }
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                try { await repository.ClearProcessingLockAsync(message.Id, CancellationToken.None); }
                catch (Exception clearEx) { _logger.LogWarning(clearEx, "[Phantom] Failed to clear processing lock for outbox message {MessageId} on shutdown", message.Id); }
                throw;
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "[Phantom] Failed to publish outbox message {MessageId} after retries", message.Id);
                await SafeIncrementRetryAsync(repository, message.Id, publishEx.Message, ct);

                if (message.RetryCount + 1 >= message.MaxRetryCount)
                {
                    _logger.LogCritical("[Phantom] Outbox message {MessageId} reached terminal failure after {RetryCount} retries. Will no longer be retried.",
                        message.Id, message.RetryCount + 1);
                }
                return;
            }

            try
            {
                await repository.MarkAsPublishedAsync(message.Id, ct);
                _logger.LogInformation("[Phantom] Outbox message {MessageId} published successfully", message.Id);
            }
            catch (Exception markEx)
            {
                _logger.LogCritical(markEx,
                    "[Phantom] Outbox message {MessageId} was published to broker but could not be marked as published in database. " +
                    "This may cause a duplicate publish on the next iteration. Manual intervention may be required.",
                    message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Phantom] Unexpected error processing outbox message {MessageId}", message.Id);
            await SafeIncrementRetryAsync(repository, message.Id, ex.Message, ct);
        }
        finally
        {
            try
            {
                await repository.ClearProcessingLockAsync(message.Id, ct);
            }
            catch (Exception clearEx)
            {
                _logger.LogWarning(clearEx, "[Phantom] Failed to clear processing lock for outbox message {MessageId}", message.Id);
            }
        }
    }

    private async Task SafeIncrementRetryAsync(IOutboxMessageRepository repository, Guid messageId, string error, CancellationToken ct)
    {
        try
        {
            await repository.IncrementRetryCountAsync(messageId, error, ct);
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "[Phantom] Failed to increment retry count for outbox message {MessageId}", messageId);
        }
    }

    private async Task SafeMarkTerminalFailureAsync(IOutboxMessageRepository repository, Guid messageId, string error, CancellationToken ct)
    {
        try
        {
            await repository.MarkAsTerminalFailureAsync(messageId, error, ct);
            _logger.LogCritical("[Phantom] Outbox message {MessageId} marked as terminal failure: {Error}", messageId, error);
        }
        catch (Exception termEx)
        {
            _logger.LogError(termEx, "[Phantom] Failed to mark outbox message {MessageId} as terminal failure", messageId);
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
