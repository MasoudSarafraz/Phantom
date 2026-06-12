using FluentValidation;
using FluentValidation.Results;

namespace Phantom.CQRS.Pipelines;

public class ValidationPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in _validators)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        await next();
    }
}
