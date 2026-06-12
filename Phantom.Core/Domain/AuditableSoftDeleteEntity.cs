namespace Phantom.Core.Domain;

public abstract class AuditableSoftDeleteEntity<TId> : AuditableEntity<TId>
{
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }
    public string? DeletedBy { get; protected set; }

    protected AuditableSoftDeleteEntity() { }
    protected AuditableSoftDeleteEntity(TId id) : base(id) { }

    public virtual void SoftDelete(string? deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }

    public virtual void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
