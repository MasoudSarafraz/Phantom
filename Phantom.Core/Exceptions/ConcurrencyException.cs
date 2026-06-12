namespace Phantom.Core.Exceptions;

public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string entityName, object entityId)
        : base($"Concurrency conflict detected for entity '{entityName}' with id '{entityId}'. The entity has been modified by another request.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }

    public string EntityName { get; }
    public object EntityId { get; }
}
