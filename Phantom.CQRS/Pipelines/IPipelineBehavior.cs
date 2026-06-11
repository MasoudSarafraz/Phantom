namespace Phantom.CQRS.Pipelines;

public interface IPipelineBehavior<in TRequest> where TRequest : notnull
{
    Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}