namespace Phantom.Core.Domain;

public abstract class SoftDeleteEntity<TId> : Entity<TId>
{
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }

    protected SoftDeleteEntity() { }
    protected SoftDeleteEntity(TId id) : base(id) { }

    public virtual void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public virtual void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
    }
}
