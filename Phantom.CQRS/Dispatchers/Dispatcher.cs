using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Exceptions;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;

namespace Phantom.CQRS.Dispatchers;

/// <summary>
/// Default implementation of <see cref="IDispatcher"/> that resolves handlers
/// from the DI container and supports pipeline behaviors for both commands and queries.
/// Reflection results are cached statically to avoid repeated <see cref="Type.MakeGenericType"/>
/// and <see cref="Type.GetMethod"/> calls on every dispatch.
/// </summary>
public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Cache for command-with-result handler types and their <c>HandleAsync</c> method info,
    /// keyed by the concrete command type and result type.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResultType), (Type HandlerType, MethodInfo HandleMethod)> _commandResultHandlerCache = new();

    /// <summary>
    /// Cache for query handler types and their <c>HandleAsync</c> method info,
    /// keyed by the concrete query type and result type.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResultType), (Type HandlerType, MethodInfo HandleMethod)> _queryHandlerCache = new();

    /// <summary>
    /// Cache for pipeline behavior interface types and their <c>HandleAsync</c> method info,
    /// keyed by the request type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, (Type InterfaceType, MethodInfo HandleMethod)> _pipelineCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Dispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve handlers and pipeline behaviors.</param>
    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var (handlerType, handleMethod) = _commandResultHandlerCache.GetOrAdd((commandType, typeof(TResult)), key =>
        {
            var ht = typeof(ICommandHandler<,>).MakeGenericType(key.RequestType, key.ResultType);
            var hm = ht.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method 'HandleAsync' not found on handler type '{ht.FullName}'. This is an internal error; the interface contract may have changed.");
            return (ht, hm);
        });

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService(handlerType)
            ?? throw new HandlerNotFoundException(commandType, $"No handler registered for command type '{commandType.FullName}' with result type '{typeof(TResult).FullName}'.");

        Task<TResult> HandlerDelegate() => (Task<TResult>)handleMethod.Invoke(handler, new object[] { command, cancellationToken })!;

        var currentDelegate = (Func<Task<TResult>>)HandlerDelegate;

        var (pipelineInterfaceType, pipelineHandleMethod) = _pipelineCache.GetOrAdd(commandType, ct =>
        {
            var pt = typeof(IPipelineBehavior<>).MakeGenericType(ct);
            var pm = pt.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method 'HandleAsync' not found on pipeline behavior type '{pt.FullName}'. This is an internal error; the interface contract may have changed.");
            return (pt, pm);
        });

        var pipelineBehaviors = scope.ServiceProvider.GetServices(pipelineInterfaceType).Cast<object>().Reverse().ToList();

        foreach (var behavior in pipelineBehaviors)
        {
            var current = currentDelegate;
            currentDelegate = () =>
            {
                var requestHandlerDelegate = new RequestHandlerDelegate(() => current());
                return (Task<TResult>)pipelineHandleMethod.Invoke(behavior, new object[] { command, requestHandlerDelegate, cancellationToken })!;
            };
        }

        return await currentDelegate();
    }

    /// <inheritdoc/>
    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var queryType = query.GetType();
        var (handlerType, handleMethod) = _queryHandlerCache.GetOrAdd((queryType, typeof(TResult)), key =>
        {
            var ht = typeof(IQueryHandler<,>).MakeGenericType(key.RequestType, key.ResultType);
            var hm = ht.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method 'HandleAsync' not found on handler type '{ht.FullName}'. This is an internal error; the interface contract may have changed.");
            return (ht, hm);
        });

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService(handlerType)
            ?? throw new HandlerNotFoundException(queryType, $"No handler registered for query type '{queryType.FullName}' with result type '{typeof(TResult).FullName}'.");

        Task<TResult> HandlerDelegate() => (Task<TResult>)handleMethod.Invoke(handler, new object[] { query, cancellationToken })!;

        var currentDelegate = (Func<Task<TResult>>)HandlerDelegate;

        var (pipelineInterfaceType, pipelineHandleMethod) = _pipelineCache.GetOrAdd(queryType, qt =>
        {
            var pt = typeof(IPipelineBehavior<>).MakeGenericType(qt);
            var pm = pt.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Method 'HandleAsync' not found on pipeline behavior type '{pt.FullName}'. This is an internal error; the interface contract may have changed.");
            return (pt, pm);
        });

        var pipelineBehaviors = scope.ServiceProvider.GetServices(pipelineInterfaceType).Cast<object>().Reverse().ToList();

        foreach (var behavior in pipelineBehaviors)
        {
            var current = currentDelegate;
            currentDelegate = () =>
            {
                var requestHandlerDelegate = new RequestHandlerDelegate(() => current());
                return (Task<TResult>)pipelineHandleMethod.Invoke(behavior, new object[] { query, requestHandlerDelegate, cancellationToken })!;
            };
        }

        return await currentDelegate();
    }
}
