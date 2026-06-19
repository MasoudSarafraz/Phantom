namespace Phantom.Core.Domain;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public int Version { get; protected set; }

    protected Entity() { }

    protected Entity(TId id)
    {
        Id = id;
    }

    public bool IsTransient()
    {
        return EqualityComparer<TId>.Default.Equals(Id, default!);
    }

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

    public override int GetHashCode()
    {
        // For transient entities (not yet persisted, Id == default), fall back to
        // reference identity so distinct transient entities do not collide inside
        // hash-based collections. Once persisted, hash by (Type, Id) as required
        // by the Entity equality contract.
        if (IsTransient())
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }

        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}
