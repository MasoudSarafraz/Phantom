namespace Phantom.Core.Domain;

public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    /// <summary>
    /// Marks the entity as soft-deleted. This method is called by the SoftDeleteInterceptor
    /// and should set <see cref="IsDeleted"/> to true and <see cref="DeletedAt"/> to the current time.
    /// </summary>
    void SoftDelete();
}
