namespace Phantom.Core.Exceptions;

/// <summary>
/// Exception thrown when an entity cannot be found by its identifier.
/// </summary>
public class NotFoundException : DomainException
{
    /// <summary>
    /// Gets the name of the entity type that was not found.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the identifier of the entity that was not found.
    /// </summary>
    public object EntityId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="entityName">The name of the entity type. Must not be <c>null</c> or empty.</param>
    /// <param name="entityId">The identifier of the entity. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityId"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is <c>null</c> or empty.</exception>
    public NotFoundException(string entityName, object entityId)
        : base($"Entity '{entityName}' with id '{entityId}' was not found.")
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentException.ThrowIfNullOrEmpty(entityName);

        EntityName = entityName;
        EntityId = entityId;
    }
}
