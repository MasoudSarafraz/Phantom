using Phantom.Core.Events;

namespace Phantom.Core.Domain;

/// <summary>
/// Non-generic marker interface for aggregate roots.
/// Enables domain event collection without relying on specific TId type parameters.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets the domain events that have been raised but not yet dispatched.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears all domain events after they have been dispatched.
    /// </summary>
    void ClearDomainEvents();
}
