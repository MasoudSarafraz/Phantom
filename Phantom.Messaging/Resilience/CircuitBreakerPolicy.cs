using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Phantom.Messaging.Resilience;

public class CircuitBreakerPolicy
{
    private readonly ILogger<CircuitBreakerPolicy> _logger;
    private readonly ResiliencePipeline _pipeline;
    public int FailureThreshold { get; }
    public TimeSpan ResetTimeout { get; }

    public CircuitBreakerPolicy(int failureThreshold, TimeSpan resetTimeout, ILogger<CircuitBreakerPolicy> logger)
    {
        FailureThreshold = failureThreshold; ResetTimeout = resetTimeout; _logger = logger;
        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0, MinimumThroughput = failureThreshold, BreakDuration = resetTimeout,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                OnOpened = args => { _logger.LogWarning("[Phantom] Circuit breaker OPENED"); return default; },
                OnClosed = args => { _logger.LogInformation("[Phantom] Circuit breaker CLOSED (recovered)"); return default; },
                OnHalfOpened = args => { _logger.LogInformation("[Phantom] Circuit breaker HALF-OPEN (testing)"); return default; }
            }).Build();
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct = default) => await _pipeline.ExecuteAsync(async token => await action(), ct);
}
