namespace Phantom.Core.Domain;

/// <summary>
/// An <see cref="AggregateRoot{TId}"/> that also implements <see cref="ISoftDeletable"/>.
/// Use this base class when your aggregate should be soft-deleted (IsDeleted flag + global
/// query filter) instead of physically removed from the database.
///
/// Without this class you would be forced to inherit from <see cref="SoftDeleteEntity{TId}"/>,
/// which is not an aggregate root and therefore cannot raise domain events.
/// </summary>
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
