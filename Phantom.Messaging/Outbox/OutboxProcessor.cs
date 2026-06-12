using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Outbox;

namespace Phantom.Messaging.Outbox;

/// <summary>
/// Background service that periodically processes pending outbox messages,
/// attempting to publish them to their target channels.
/// Uses <see cref="IMessageSerializer"/> for consistent deserialization and
/// supports runtime type resolution with loaded assembly fallback.
/// </summary>
public class OutboxProcessor : BackgroundService, IOutboxProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxProcessor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving scoped dependencies.</param>
    /// <param name="serializer">The message serializer for deserializing event payloads.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="batchSize">The maximum number of messages to process per polling cycle.</param>
    /// <param name="pollingInterval">The interval between polling cycles.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is null.</exception>
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

    /// <inheritdoc />
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
                // Expected during shutdown, no need to log
            }
        }

        _logger.LogInformation("[Phantom] Outbox Processor stopped");
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Processes a single outbox message, attempting to deserialize and publish it.
    /// On failure, increments the retry count and records the error.
    /// </summary>
    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IOutboxMessageRepository repository,
        IEventPublisher publisher,
        CancellationToken ct)
    {
        try
        {
            // Resolve the event type from the stored assembly-qualified name
            var eventType = ResolveEventType(message.EventType);
            if (eventType is null)
            {
                await repository.MarkAsFailedAsync(message.Id, $"Cannot resolve type: {message.EventType}", ct);
                return;
            }

            // Deserialize using the injected IMessageSerializer for consistency
            var data = System.Text.Encoding.UTF8.GetBytes(message.Payload);
            var @event = _serializer.Deserialize(data, eventType) as IIntegrationEvent;
            if (@event is null)
            {
                await repository.MarkAsFailedAsync(message.Id, $"Deserialization returned null for type: {message.EventType}", ct);
                return;
            }

            // Publish to the specified channel or all mapped channels
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

            // Increment retry count and record the error
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

    /// <summary>
    /// Resolves a type from its assembly-qualified name, with a fallback that scans
    /// all loaded assemblies if the standard <see cref="Type.GetType"/> method fails.
    /// </summary>
    /// <param name="typeName">The assembly-qualified type name to resolve.</param>
    /// <returns>The resolved type, or null if the type cannot be found.</returns>
    private static Type? ResolveEventType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        // First attempt: standard Type.GetType with assembly-qualified name
        var eventType = Type.GetType(typeName);
        if (eventType is not null) return eventType;

        // Fallback: scan loaded assemblies for the type
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
                // Skip assemblies that throw during type resolution
            }
        }

        return null;
    }
}
