using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Abstraction over a resilience pipeline (retry + circuit breaker) used by
/// <see cref="IEventPublisher"/> and <see cref="Outbox.IOutboxProcessor"/> when
/// publishing integration events to a broker.
/// </summary>
/// <remarks>
/// The default <see cref="NullResiliencePipeline"/> does not retry or break —
/// it simply executes the supplied action. Activate retry and/or circuit breaker
/// by calling <c>ConfigureRetry</c> and <c>ConfigureCircuitBreaker</c> on
/// <c>PhantomOptions</c>.
/// </remarks>
public interface IResiliencePipeline
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op pipeline used when the user has not configured retry or circuit breaker.
/// Executes the action exactly once without any wrapping.
/// </summary>
public sealed class NullResiliencePipeline : IResiliencePipeline
{
    public static readonly NullResiliencePipeline Instance = new();

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        => await action(cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        => await action(cancellationToken);
}

/// <summary>
/// Default resilience pipeline that combines a retry strategy and a circuit breaker
/// into a single Polly <see cref="ResiliencePipeline"/>. Either strategy may be
/// omitted by passing null; in that case the pipeline still works but only applies
/// the strategies that are configured.
/// </summary>
public sealed class CompositeResiliencePipeline : IResiliencePipeline
{
    private readonly ResiliencePipeline _pipeline;

    public CompositeResiliencePipeline(
        RetryStrategyOptions? retry = null,
        CircuitBreakerStrategyOptions? circuitBreaker = null)
    {
        var builder = new ResiliencePipelineBuilder();

        if (retry is not null)
            builder.AddRetry(retry);

        if (circuitBreaker is not null)
            builder.AddCircuitBreaker(circuitBreaker);

        _pipeline = builder.Build();
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        => await _pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        => await _pipeline.ExecuteAsync(async ct => await action(ct), cancellationToken);
}
