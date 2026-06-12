namespace Phantom.Core.Domain;

/// <summary>
/// Interface for entities that support audit tracking.
/// Implemented by <see cref="AuditableEntity{TId}"/> and <see cref="AuditableSoftDeleteEntity{TId}"/>.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets the timestamp when the entity was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the identifier of the user who created the entity.
    /// </summary>
    string? CreatedBy { get; }

    /// <summary>
    /// Gets the timestamp when the entity was last updated.
    /// </summary>
    DateTimeOffset? UpdatedAt { get; }

    /// <summary>
    /// Gets the identifier of the user who last updated the entity.
    /// </summary>
    string? UpdatedBy { get; }

    /// <summary>
    /// Sets the creation audit fields.
    /// </summary>
    /// <param name="createdBy">The identifier of the user who created the entity.</param>
    void SetCreated(string? createdBy);

    /// <summary>
    /// Sets the update audit fields.
    /// </summary>
    /// <param name="updatedBy">The identifier of the user who updated the entity.</param>
    void SetUpdated(string? updatedBy);
}
