using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Phantom.CQRS.Pipelines;

/// <summary>
/// Pipeline behavior that logs request handling lifecycle events, including
/// elapsed time and differentiated handling of <see cref="OperationCanceledException"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
public class LoggingPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly ILogger<LoggingPipelineBehavior<TRequest>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingPipelineBehavior{TRequest}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for writing log messages.</param>
    public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
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
