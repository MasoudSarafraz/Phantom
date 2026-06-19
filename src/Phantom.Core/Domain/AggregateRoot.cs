using Phantom.Core.Events;
using Phantom.Core.Exceptions;
using Phantom.Core.Results;

namespace Phantom.Core.Domain;

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot, IAggregateRootPersistence where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    protected void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Checks a business rule and throws <see cref="BusinessRuleException"/> if the rule is broken.
    /// Use this in aggregate methods to enforce invariants in a structured, declarative way.
    /// </summary>
    /// <example>
    /// <code>
    /// CheckRule(new OrderMustBePendingRule(Status));
    /// CheckRule("ORDER_NOT_PENDING", "Cannot modify a confirmed order", Status != "Pending");
    /// </code>
    /// </example>
    protected void CheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IsBroken())
            throw new BusinessRuleException(rule.Message);
    }

    /// <summary>
    /// Convenience overload for inline invariant checks without creating a separate rule class.
    /// </summary>
    protected void CheckRule(string code, string message, bool isBroken)
    {
        if (isBroken)
            throw new BusinessRuleException(code, message);
    }

    /// <summary>
    /// Result-returning counterpart of <see cref="CheckRule(IBusinessRule)"/>.
    /// Returns <see cref="Result.Failure(string)"/> with the rule's message when the rule
    /// is broken, or <see cref="Result.Success()"/> otherwise. Does NOT throw.
    ///
    /// Use this when the calling aggregate method prefers to return a <see cref="Result"/>
    /// to its caller rather than throwing. The caller can then convert the result to an
    /// exception via <see cref="Result.ThrowIfFailure"/> if needed.
    /// </summary>
    protected Result TryCheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.IsBroken()
            ? Result.Failure(rule.Message)
            : Result.Success();
    }

    /// <summary>
    /// Result-returning counterpart of <see cref="CheckRule(string, string, bool)"/>.
    /// </summary>
    protected Result TryCheckRule(string code, string message, bool isBroken)
    {
        return isBroken
            ? Result.Failure($"[{code}] {message}")
            : Result.Success();
    }

    /// <summary>
    /// Clears all domain events. This method is intended for infrastructure use only
    /// (called by <c>PhantomDbContext</c> after events have been dispatched or saved to outbox).
    /// External code should not call this method directly.
    /// </summary>
    /// <remarks>
    /// Public visibility is kept for backward compatibility with code that subclasses
    /// <see cref="AggregateRoot{TId}"/> and calls <c>ClearDomainEvents</c> from inside the
    /// aggregate (which is legitimate). The <see cref="IAggregateRoot"/> public interface
    /// no longer exposes this method; infrastructure code casts to
    /// <see cref="IAggregateRootPersistence"/> via <c>[InternalsVisibleTo]</c>.
    /// </remarks>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Explicit implementation of the internal persistence interface. Infrastructure
    /// code (Phantom.Data) casts <see cref="IAggregateRoot"/> instances to
    /// <see cref="IAggregateRootPersistence"/> to clear events after SaveChanges.
    /// </summary>
    void IAggregateRootPersistence.ClearDomainEvents() => ClearDomainEvents();
}

/// <summary>
/// Interface for defining business rules that can be checked by <see cref="AggregateRoot{TId}.CheckRule"/>.
/// </summary>
public interface IBusinessRule
{
    bool IsBroken();

    string Message { get; }
}
