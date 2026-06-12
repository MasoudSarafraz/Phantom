namespace Phantom.Core.Exceptions;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict is detected — i.e., the entity
/// has been modified by another request since it was last read.
/// </summary>
public class ConcurrencyException : DomainException
{
    /// <summary>
    /// Gets the name of the entity type involved in the concurrency conflict.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the identifier of the entity involved in the concurrency conflict.
    /// </summary>
    public object EntityId { get; }

    /// <summary>
    /// Gets the version that was expected at the time of the update, or <c>null</c> if not available.
    /// </summary>
    public int? ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual version found in the data store, or <c>null</c> if not available.
    /// </summary>
    public int? ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </summary>
    /// <param name="entityName">The name of the entity type. Must not be <c>null</c> or empty.</param>
    /// <param name="entityId">The identifier of the entity. Must not be <c>null</c>.</param>
    /// <param name="expectedVersion">The version that was expected at the time of the update.</param>
    /// <param name="actualVersion">The actual version found in the data store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityId"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entityName"/> is <c>null</c> or empty.</exception>
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
