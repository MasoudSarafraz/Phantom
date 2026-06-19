namespace Phantom.CQRS.Pipelines;

public delegate Task RequestHandlerDelegate();

public delegate Task<TResult> RequestHandlerDelegate<TResult>();
