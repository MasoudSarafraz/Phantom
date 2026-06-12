using Microsoft.Extensions.DependencyInjection;

namespace Phantom.CQRS.Pipelines;

public class ValidationPipelineBehavior<TRequest> : IPipelineBehavior<TRequest> where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationPipelineBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        var validatorType = typeof(FluentValidation.IValidator<>).MakeGenericType(typeof(TRequest));
        var validators = _serviceProvider.GetServices(validatorType);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var validateMethod = validatorType.GetMethod("ValidateAsync", new[] { typeof(TRequest), typeof(CancellationToken) })!;
            var validationResultTask = (Task<FluentValidation.Results.ValidationResult>)validateMethod.Invoke(validator, new object[] { request, cancellationToken })!;
            var validationResult = await validationResultTask;
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count > 0)
            throw new FluentValidation.ValidationException(failures);

        await next();
    }
}
