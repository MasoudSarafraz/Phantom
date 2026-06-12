using Phantom.Core.Events;

namespace Phantom.Core.Domain;

/// <summary>
/// Base class for aggregate roots in the domain model. Aggregate roots are the entry points
/// to aggregates and are responsible for managing domain events and enforcing invariants.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root's unique identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the collection of domain events that have been raised by this aggregate root
    /// and are awaiting dispatch.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Adds a domain event to the aggregate root's event collection for later dispatch.
    /// </summary>
    /// <param name="domainEvent">The domain event to add. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainEvent"/> is <c>null</c>.</exception>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes a domain event from the aggregate root's event collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    protected void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Clears all domain events from the aggregate root's event collection.
    /// Typically called after events have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
