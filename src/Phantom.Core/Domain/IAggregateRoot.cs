using Phantom.Core.Events;

namespace Phantom.Core.Domain;

/// <summary>
/// Public contract of an aggregate root. Only exposes the read-only view of pending
/// domain events. Clearing events is intentionally NOT part of the public API —
/// see <see cref="IAggregateRootPersistence"/> for the infrastructure-only pathway.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
}

/// <summary>
/// Internal interface for infrastructure-only access to clear domain events.
/// This prevents external code from accidentally clearing events while allowing
/// <c>PhantomDbContext</c> (in <c>Phantom.Data</c>) to do so after persistence
/// via <c>[InternalsVisibleTo]</c>.
/// </summary>
internal interface IAggregateRootPersistence
{
    void ClearDomainEvents();
}
