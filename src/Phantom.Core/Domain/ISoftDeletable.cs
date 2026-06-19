namespace Phantom.Core.Domain;

public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    void SoftDelete();
}
