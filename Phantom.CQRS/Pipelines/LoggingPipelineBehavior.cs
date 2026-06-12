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
        _logger.LogInformation("[Phantom] Handling {RequestName}", requestName);
        try
        {
            await next();
            _logger.LogInformation("[Phantom] Handled {RequestName} successfully", requestName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Phantom] Failed to handle {RequestName}", requestName);
            throw;
        }
    }
}
