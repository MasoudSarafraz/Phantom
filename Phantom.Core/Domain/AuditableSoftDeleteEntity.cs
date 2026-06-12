namespace Phantom.Core.Domain;

/// <summary>
/// Base class for entities that support both audit tracking and soft deletion.
/// Combines the functionality of <see cref="AuditableEntity{TId}"/> and <see cref="ISoftDeletable"/>
/// to provide comprehensive lifecycle management.
/// </summary>
/// <typeparam name="TId">The type of the entity's unique identifier.</typeparam>
public abstract class AuditableSoftDeleteEntity<TId> : AuditableEntity<TId>, ISoftDeletable where TId : notnull
{
    /// <summary>
    /// Gets a value indicating whether this entity has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// Gets the date and time when this entity was soft-deleted, or <c>null</c> if not deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; protected set; }

    /// <summary>
    /// Gets the identity of the user who soft-deleted this entity, or <c>null</c> if not available.
    /// </summary>
    public string? DeletedBy { get; protected set; }

    protected AuditableSoftDeleteEntity() { }

    protected AuditableSoftDeleteEntity(TId id) : base(id) { }

    /// <summary>
    /// Marks this entity as soft-deleted and records the deletion timestamp and user.
    /// This operation is idempotent — calling it on an already-deleted entity has no effect.
    /// </summary>
    /// <param name="deletedBy">The identity of the user who performed the deletion.</param>
    public virtual void SoftDelete(string? deletedBy)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    /// <summary>
    /// Restores a previously soft-deleted entity by clearing the deletion flag.
    /// Preserves the <see cref="DeletedAt"/> and <see cref="DeletedBy"/> fields for audit trail purposes.
    /// This operation is idempotent — calling it on a non-deleted entity has no effect.
    /// </summary>
    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        // Intentionally preserve DeletedAt and DeletedBy for audit trail
    }
}
