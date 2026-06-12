namespace Phantom.Core.Domain;

/// <summary>
/// Interface for entities that support soft deletion.
/// Implemented by <see cref="SoftDeleteEntity{TId}"/> and <see cref="AuditableSoftDeleteEntity{TId}"/>.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets a value indicating whether the entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Gets the timestamp when the entity was soft-deleted, or null if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAt { get; }
}
