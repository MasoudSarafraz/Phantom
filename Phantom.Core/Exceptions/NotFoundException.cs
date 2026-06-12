namespace Phantom.Core.Exceptions;

public class NotFoundException : DomainException
{
    public string EntityName { get; }

    public object EntityId { get; }

    public NotFoundException(string entityName, object entityId)
        : base($"Entity '{entityName}' with id '{entityId}' was not found.")
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        EntityName = entityName;
        EntityId = entityId;
    }
}
