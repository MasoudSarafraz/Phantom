using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Phantom.CQRS.Pipelines;

public class LoggingPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly ILogger<LoggingPipelineBehavior<TRequest>> _logger;

    public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest>> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[Phantom] Handling {RequestName}", requestName);

        try
        {
            await next();
            stopwatch.Stop();
            _logger.LogInformation("[Phantom] Handled {RequestName} successfully in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "[Phantom] Handling of {RequestName} was canceled after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[Phantom] Failed to handle {RequestName} after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
