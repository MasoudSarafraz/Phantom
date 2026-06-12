namespace Phantom.Core.Domain;

/// <summary>
/// Base class for all entities in the domain model. Provides identity-based equality
/// and comparison semantics consistent with Domain-Driven Design principles.
/// </summary>
/// <typeparam name="TId">The type of the entity's unique identifier. Must be a non-nullable type.</typeparam>
public abstract class Entity<TId> where TId : notnull
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Gets or sets the version number used for optimistic concurrency control.
    /// </summary>
    public int Version { get; protected set; }

    protected Entity() { }

    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Determines whether this entity is transient (i.e., has not been persisted yet).
    /// A transient entity has an identifier equal to the default value for its type.
    /// </summary>
    /// <returns><c>true</c> if the entity is transient; otherwise, <c>false</c>.</returns>
    public bool IsTransient()
    {
        return EqualityComparer<TId>.Default.Equals(Id, default!);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// Two transient entities are only considered equal if they are the same reference.
    /// Two non-transient entities are considered equal if they have the same type and identifier.
    /// </summary>
    /// <param name="obj">The object to compare with the current entity.</param>
    /// <returns><c>true</c> if the specified object is equal to the current entity; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (IsTransient() && other.IsTransient())
            return ReferenceEquals(this, other);

        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Returns a hash code for the current entity based on its identifier and type.
    /// </summary>
    /// <returns>A hash code for the current entity.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    /// <summary>
    /// Determines whether two entities are equal.
    /// </summary>
    /// <param name="left">The first entity to compare.</param>
    /// <param name="right">The second entity to compare.</param>
    /// <returns><c>true</c> if the entities are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Determines whether two entities are not equal.
    /// </summary>
    /// <param name="left">The first entity to compare.</param>
    /// <param name="right">The second entity to compare.</param>
    /// <returns><c>true</c> if the entities are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}
