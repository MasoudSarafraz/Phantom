namespace Phantom.Core.Domain;

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    string? CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    string? UpdatedBy { get; }

    void SetCreated(string? createdBy);

    void SetUpdated(string? updatedBy);
}
