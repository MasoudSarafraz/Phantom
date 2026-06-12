using FluentValidation;
using FluentValidation.Results;

namespace Phantom.CQRS.Pipelines;

/// <summary>
/// Pipeline behavior that validates requests using FluentValidation before
/// passing them to the next handler in the pipeline.
/// Validators are injected directly via <see cref="IValidator{TRequest}"/>
/// to avoid runtime reflection overhead.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public class ValidationPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TRequest}"/> class.
    /// </summary>
    /// <param name="validators">
    /// All <see cref="IValidator{TRequest}"/> instances registered for the request type.
    /// The DI container will automatically resolve all applicable validators.
    /// </param>
    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc/>
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
