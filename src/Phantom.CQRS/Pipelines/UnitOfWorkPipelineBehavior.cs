using Phantom.Core.Services;
using Phantom.CQRS.Pipelines;

namespace Phantom.CQRS.Pipelines;

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
