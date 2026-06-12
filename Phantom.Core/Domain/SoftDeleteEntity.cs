namespace Phantom.Core.Domain;

/// <summary>
/// Base class for entities that support soft deletion. Soft-deleted entities are marked
/// as deleted rather than physically removed from the data store, allowing for data recovery
/// and audit trail preservation.
/// </summary>
/// <typeparam name="TId">The type of the entity's unique identifier.</typeparam>
public abstract class SoftDeleteEntity<TId> : Entity<TId>, ISoftDeletable where TId : notnull
{
    /// <summary>
    /// Gets a value indicating whether this entity has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// Gets the date and time when this entity was soft-deleted, or <c>null</c> if not deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; protected set; }

    protected SoftDeleteEntity() { }

    protected SoftDeleteEntity(TId id) : base(id) { }

    /// <summary>
    /// Marks this entity as soft-deleted and records the deletion timestamp.
    /// This operation is idempotent — calling it on an already-deleted entity has no effect.
    /// </summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Restores a previously soft-deleted entity by clearing the deletion flag.
    /// This operation is idempotent — calling it on a non-deleted entity has no effect.
    /// </summary>
    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletedAt = null;
    }
}
