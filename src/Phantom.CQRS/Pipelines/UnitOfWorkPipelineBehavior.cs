using Phantom.Core.Services;
using Phantom.CQRS.Pipelines;

namespace Phantom.CQRS.Pipelines;

/// <summary>
/// Pipeline behavior that automatically saves changes via UnitOfWork after
/// successful command handling. This eliminates the repetitive
/// <c>await _unitOfWork.SaveChangesAsync()</c> boilerplate in every command handler.
///
/// This behavior is registered by Phantom.NET when calling AddPhantom()
/// and is resolved from the root DI container where IUnitOfWork is available.
/// </summary>
public class UnitOfWorkPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkPipelineBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        await next();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
