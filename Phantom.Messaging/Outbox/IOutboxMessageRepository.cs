namespace Phantom.Messaging.Outbox;

/// <summary>
/// Defines a repository for persisting and retrieving outbox messages,
/// supporting the transactional outbox pattern for reliable event publishing.
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Adds a new outbox message to the repository.
    /// </summary>
    /// <param name="message">The outbox message to add.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);

    /// <summary>
    /// Retrieves pending outbox messages that have not yet been published, ordered by creation time.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to retrieve.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of pending outbox messages.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Retrieves failed outbox messages that have not exceeded the maximum retry count.
    /// Used for retry processing of previously failed messages.
    /// </summary>
    /// <param name="maxRetryCount">The maximum retry count threshold; only messages with RetryCount less than this value are returned.</param>
    /// <param name="batchSize">The maximum number of messages to retrieve.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of failed outbox messages eligible for retry.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetFailedAsync(int maxRetryCount, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox message as successfully published.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to mark as published.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox message as failed, recording the error message.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to mark as failed.</param>
    /// <param name="error">A description of the error that caused the failure.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default);

    /// <summary>
    /// Increments the retry count for a message and records the error from the latest failure.
    /// Used to track retry attempts and implement backoff strategies.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to update.</param>
    /// <param name="error">A description of the error from the latest failure.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    Task IncrementRetryCountAsync(Guid messageId, string error, CancellationToken ct = default);
}
