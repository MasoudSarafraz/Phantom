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

    protected void CheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IsBroken())
            throw new BusinessRuleException(rule.Message);
    }

    protected void CheckRule(string code, string message, bool isBroken)
    {
        if (isBroken)
            throw new BusinessRuleException(code, message);
    }

    protected Result TryCheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.IsBroken()
            ? Result.Failure(rule.Message)
            : Result.Success();
    }

    protected Result TryCheckRule(string code, string message, bool isBroken)
    {
        return isBroken
            ? Result.Failure($"[{code}] {message}")
            : Result.Success();
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    void IAggregateRootPersistence.ClearDomainEvents() => ClearDomainEvents();
}

public interface IBusinessRule
{
    bool IsBroken();

    string Message { get; }
}
