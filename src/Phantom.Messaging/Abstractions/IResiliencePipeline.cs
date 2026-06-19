using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Phantom.Messaging.Abstractions;

public interface IResiliencePipeline
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public sealed class NullResiliencePipeline : IResiliencePipeline
{
    public static readonly NullResiliencePipeline Instance = new();

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        => await action(cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        => await action(cancellationToken);
}

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
