namespace Phantom.Core.Domain;

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

    public virtual void SoftDelete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

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
