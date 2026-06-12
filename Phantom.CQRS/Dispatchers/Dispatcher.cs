using Microsoft.Extensions.DependencyInjection;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;

namespace Phantom.CQRS.Dispatchers;

public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand>>();
        var pipelineBehaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TCommand>>().Reverse().ToList();

        RequestHandlerDelegate handlerDelegate = () => handler.HandleAsync(command, cancellationToken);

        foreach (var behavior in pipelineBehaviors)
        {
            var currentHandler = handlerDelegate;
            handlerDelegate = () => behavior.HandleAsync(command, currentHandler, cancellationToken);
        }

        await handlerDelegate();
    }

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = scope.ServiceProvider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod("HandleAsync")!;

        Task<TResult> HandlerDelegate() => (Task<TResult>)handleMethod.Invoke(handler, new object[] { command, cancellationToken })!;

        var currentDelegate = HandlerDelegate;

        var pipelineBehaviorType = typeof(IPipelineBehavior<>).MakeGenericType(command.GetType());
        var pipelineBehaviors = scope.ServiceProvider.GetServices(pipelineBehaviorType).Cast<object>().Reverse().ToList();

        foreach (var behavior in pipelineBehaviors)
        {
            var current = currentDelegate;
            var handleAsyncMethod = behavior.GetType().GetMethod("HandleAsync")!;
            currentDelegate = () =>
            {
                var requestHandlerDelegate = new RequestHandlerDelegate(() => current());
                return (Task<TResult>)handleAsyncMethod.Invoke(behavior, new object[] { command, requestHandlerDelegate, cancellationToken })!;
            };
        }

        return await currentDelegate();
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = scope.ServiceProvider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod("HandleAsync")!;
        return await (Task<TResult>)handleMethod.Invoke(handler, new object[] { query, cancellationToken })!;
    }
}
