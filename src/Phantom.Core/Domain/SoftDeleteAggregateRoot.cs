namespace Phantom.Core.Domain;

public abstract class SoftDeleteAggregateRoot<TId> : AggregateRoot<TId>, ISoftDeletable where TId : notnull
{
    public bool IsDeleted { get; protected set; }

    public DateTimeOffset? DeletedAt { get; protected set; }

    protected SoftDeleteAggregateRoot() { }

    protected SoftDeleteAggregateRoot(TId id) : base(id) { }

    public virtual void SoftDelete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletedAt = null;
    }
}
