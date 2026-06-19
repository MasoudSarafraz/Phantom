using Phantom.Core.Events;

namespace Phantom.Core.Domain;

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
}

internal interface IAggregateRootPersistence
{
    void ClearDomainEvents();
}
