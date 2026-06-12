namespace Phantom.Core.Domain;

public abstract class AuditableEntity<TId> : Entity<TId> where TId : notnull
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public string? CreatedBy { get; protected set; }

    public DateTimeOffset? UpdatedAt { get; protected set; }

    public string? UpdatedBy { get; protected set; }

    protected AuditableEntity() { }

    protected AuditableEntity(TId id) : base(id) { }

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
