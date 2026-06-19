namespace Phantom.Core.Domain;

public abstract class AuditableSoftDeleteEntity<TId> : AuditableEntity<TId>, ISoftDeletable where TId : notnull
{
    public bool IsDeleted { get; protected set; }

    public DateTimeOffset? DeletedAt { get; protected set; }

    public string? DeletedBy { get; protected set; }

    protected AuditableSoftDeleteEntity() { }

    protected AuditableSoftDeleteEntity(TId id) : base(id) { }

    /// <summary>
    /// Marks the entity as soft-deleted (ISoftDeletable implementation).
    /// Sets DeletedBy to null — use <see cref="SoftDelete(string?)"/> overload
    /// to record which user performed the deletion.
    /// </summary>
    public virtual void SoftDelete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the entity as soft-deleted and records who performed the deletion.
    /// </summary>
    public virtual void SoftDelete(string? deletedBy)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
