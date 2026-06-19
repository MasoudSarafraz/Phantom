using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Phantom.Messaging.Resilience;

public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly ResiliencePipeline _pipeline;
    public int MaxRetries { get; }
    public TimeSpan BaseDelay { get; }

    public RetryPolicy(int maxRetries, TimeSpan baseDelay, ILogger<RetryPolicy> logger)
    {
        MaxRetries = maxRetries; BaseDelay = baseDelay; _logger = logger;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries, Delay = baseDelay, BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnRetry = args => { _logger.LogWarning("[Phantom] Retry attempt {Attempt}/{Max}", args.AttemptNumber + 1, maxRetries); return default; }
            }).Build();
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct = default) => await _pipeline.ExecuteAsync(async token => await action(), ct);
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => await _pipeline.ExecuteAsync(async token => await action(), ct);
}
