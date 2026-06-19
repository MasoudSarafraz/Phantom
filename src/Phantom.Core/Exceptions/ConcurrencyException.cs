namespace Phantom.Core.Exceptions;

public class ConcurrencyException : DomainException
{
    public string EntityName { get; }

    public object EntityId { get; }

    public int? ExpectedVersion { get; }

    public int? ActualVersion { get; }

    public ConcurrencyException(string entityName, object entityId, int? expectedVersion = null, int? actualVersion = null)
        : base($"Concurrency conflict detected for entity '{entityName}' with id '{entityId}'. The entity has been modified by another request.")
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        EntityName = entityName;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
