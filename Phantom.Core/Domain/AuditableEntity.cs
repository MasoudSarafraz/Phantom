namespace Phantom.Core.Domain;

/// <summary>
/// Base class for entities that support audit tracking. Automatically records
/// creation and modification timestamps and the identity of the user who performed
/// each operation.
/// </summary>
/// <typeparam name="TId">The type of the entity's unique identifier.</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId> where TId : notnull
{
    /// <summary>
    /// Gets the date and time when this entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>
    /// Gets the identity of the user who created this entity, or <c>null</c> if not available.
    /// </summary>
    public string? CreatedBy { get; protected set; }

    /// <summary>
    /// Gets the date and time when this entity was last updated, or <c>null</c> if never updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; protected set; }

    /// <summary>
    /// Gets the identity of the user who last updated this entity, or <c>null</c> if not available.
    /// </summary>
    public string? UpdatedBy { get; protected set; }

    protected AuditableEntity() { }

    protected AuditableEntity(TId id) : base(id) { }

    /// <summary>
    /// Sets the creation audit information for this entity.
    /// This method is idempotent — calling it more than once has no effect after the first call.
    /// </summary>
    /// <param name="createdBy">The identity of the user who created this entity.</param>
    public void SetCreated(string? createdBy)
    {
        if (CreatedAt != default) return;

        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Sets the modification audit information for this entity.
    /// </summary>
    /// <param name="updatedBy">The identity of the user who updated this entity.</param>
    public void SetUpdated(string? updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}
