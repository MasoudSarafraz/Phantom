using Microsoft.Extensions.DependencyInjection;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Exceptions;
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
        var handler = scope.ServiceProvider.GetService<ICommandHandler<TCommand>>()
            ?? throw new HandlerNotFoundException(typeof(TCommand), $"No handler registered for command type '{typeof(TCommand).FullName}'.");

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
        var commandType = command.GetType();

        using var scope = _serviceProvider.CreateScope();

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        var handler = scope.ServiceProvider.GetService(handlerType)
            ?? throw new HandlerNotFoundException(commandType, $"No handler registered for command type '{commandType.FullName}' with result type '{typeof(TResult).FullName}'.");

        var handleMethod = handlerType.GetMethod("HandleAsync")!;

        Func<Task<TResult>> currentDelegate = () => (Task<TResult>)handleMethod.Invoke(handler, new object[] { command, cancellationToken })!;

        var pipelineInterfaceType = typeof(IPipelineBehavior<>).MakeGenericType(commandType);
        var pipelineHandleMethod = pipelineInterfaceType.GetMethod("HandleAsync")!;
        var pipelineBehaviors = scope.ServiceProvider.GetServices(pipelineInterfaceType).Cast<object>().Reverse().ToList();

        foreach (var behavior in pipelineBehaviors)
        {
            var current = currentDelegate;
            currentDelegate = () =>
            {
                var tcs = new TaskCompletionSource<TResult>();
                var next = new RequestHandlerDelegate(async () =>
                {
                    var result = await current();
                    tcs.SetResult(result);
                });
                var pipelineTask = (Task)pipelineHandleMethod.Invoke(behavior, new object[] { command, next, cancellationToken })!;
                return AwaitPipelineAndGetResult(pipelineTask, tcs);
            };
        }

        return await currentDelegate();
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var queryType = query.GetType();

        using var scope = _serviceProvider.CreateScope();

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        var handler = scope.ServiceProvider.GetService(handlerType)
            ?? throw new HandlerNotFoundException(queryType, $"No handler registered for query type '{queryType.FullName}' with result type '{typeof(TResult).FullName}'.");

        var handleMethod = handlerType.GetMethod("HandleAsync")!;

        Func<Task<TResult>> currentDelegate = () => (Task<TResult>)handleMethod.Invoke(handler, new object[] { query, cancellationToken })!;

        var pipelineInterfaceType = typeof(IPipelineBehavior<>).MakeGenericType(queryType);
        var pipelineHandleMethod = pipelineInterfaceType.GetMethod("HandleAsync")!;
        var pipelineBehaviors = scope.ServiceProvider.GetServices(pipelineInterfaceType).Cast<object>().Reverse().ToList();

        foreach (var behavior in pipelineBehaviors)
        {
            var current = currentDelegate;
            currentDelegate = () =>
            {
                var tcs = new TaskCompletionSource<TResult>();
                var next = new RequestHandlerDelegate(async () =>
                {
                    var result = await current();
                    tcs.SetResult(result);
                });
                var pipelineTask = (Task)pipelineHandleMethod.Invoke(behavior, new object[] { query, next, cancellationToken })!;
                return AwaitPipelineAndGetResult(pipelineTask, tcs);
            };
        }

        return await currentDelegate();
    }

    private static async Task<TResult> AwaitPipelineAndGetResult<TResult>(Task pipelineTask, TaskCompletionSource<TResult> tcs)
    {
        await pipelineTask;
        return await tcs.Task;
    }
}
