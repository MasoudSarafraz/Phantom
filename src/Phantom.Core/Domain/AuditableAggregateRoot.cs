namespace Phantom.Core.Domain;

/// <summary>
/// An <see cref="AggregateRoot{TId}"/> that also implements <see cref="IAuditable"/>.
/// Use this base class when your aggregate needs CreatedAt/UpdatedAt/CreatedBy/UpdatedBy
/// audit fields automatically populated by <c>AuditableInterceptor</c>.
///
/// Without this class you would be forced to inherit from <see cref="AuditableEntity{TId}"/>,
/// which is not an aggregate root and therefore cannot raise domain events.
/// </summary>
public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>, IAuditable where TId : notnull
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public string? CreatedBy { get; protected set; }

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    protected AuditableAggregateRoot() { }

    protected AuditableAggregateRoot(TId id) : base(id) { }

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
}
