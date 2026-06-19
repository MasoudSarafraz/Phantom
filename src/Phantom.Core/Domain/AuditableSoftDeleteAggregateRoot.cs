namespace Phantom.Core.Domain;

/// <summary>
/// An <see cref="AggregateRoot{TId}"/> that implements both <see cref="IAuditable"/> and
/// <see cref="ISoftDeletable"/>. Use this base class when your aggregate needs audit fields
/// AND soft-delete support — for example a Customer or Product aggregate that must be
/// soft-removable while keeping track of who last modified it.
///
/// Without this class you would be forced to inherit from
/// <see cref="AuditableSoftDeleteEntity{TId}"/>, which is not an aggregate root and
/// therefore cannot raise domain events.
/// </summary>
public abstract class AuditableSoftDeleteAggregateRoot<TId> : AggregateRoot<TId>, IAuditable, ISoftDeletable where TId : notnull
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public string? CreatedBy { get; protected set; }

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    public bool IsDeleted { get; protected set; }

    public DateTimeOffset? DeletedAt { get; protected set; }

    public string? DeletedBy { get; protected set; }

    protected AuditableSoftDeleteAggregateRoot() { }

    protected AuditableSoftDeleteAggregateRoot(TId id) : base(id) { }

    public void SetCreated(string? createdBy)
    {
        if (CreatedAt != default) return;

        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public void SetUpdated(string? updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Marks the aggregate as soft-deleted (ISoftDeletable implementation).
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
    /// Marks the aggregate as soft-deleted and records who performed the deletion.
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
