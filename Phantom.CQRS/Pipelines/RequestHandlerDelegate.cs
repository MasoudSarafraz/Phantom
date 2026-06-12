namespace Phantom.CQRS.Pipelines;

/// <summary>
/// Represents the next action in the request pipeline.
/// Invoking this delegate continues execution towards the eventual request handler.
/// </summary>
public delegate Task RequestHandlerDelegate();
