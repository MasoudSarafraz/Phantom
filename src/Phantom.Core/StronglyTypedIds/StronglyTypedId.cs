namespace Phantom.Core.StronglyTypedIds;

public interface IStronglyTypedId<out T> where T : notnull
{
    T Value { get; }
}

public interface IStronglyTypedId : IStronglyTypedId<object>
{
}

[Serializable]
public readonly struct StronglyTypedId<T> : IStronglyTypedId<T>, IEquatable<StronglyTypedId<T>>, IComparable<StronglyTypedId<T>>
    where T : notnull, IComparable<T>
{
    public T Value { get; }

    public StronglyTypedId(T value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(StronglyTypedId<T> other) => Equals(Value, other.Value);

    public int CompareTo(StronglyTypedId<T> other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj) => obj is StronglyTypedId<T> other && Equals(other);

    public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode();

    public override string ToString() => Value.ToString() ?? string.Empty;

    public static bool operator ==(StronglyTypedId<T> left, StronglyTypedId<T> right) => left.Equals(right);

    public static bool operator !=(StronglyTypedId<T> left, StronglyTypedId<T> right) => !left.Equals(right);

    public static bool operator <(StronglyTypedId<T> left, StronglyTypedId<T> right) => left.CompareTo(right) < 0;

    public static bool operator >(StronglyTypedId<T> left, StronglyTypedId<T> right) => left.CompareTo(right) > 0;

    public static bool operator <=(StronglyTypedId<T> left, StronglyTypedId<T> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(StronglyTypedId<T> left, StronglyTypedId<T> right) => left.CompareTo(right) >= 0;

    public static implicit operator T(StronglyTypedId<T> id) => id.Value;

    public static implicit operator StronglyTypedId<T>(T value) => new(value);
}

public readonly struct GuidId : IStronglyTypedId<Guid>, IEquatable<GuidId>, IComparable<GuidId>
{
    public Guid Value { get; }

    public GuidId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guid ID must not be empty.", nameof(value));
        Value = value;
    }

    public static GuidId New() => new(Guid.NewGuid());

    public bool Equals(GuidId other) => Value == other.Value;

    public int CompareTo(GuidId other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj) => obj is GuidId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(GuidId left, GuidId right) => left.Equals(right);

    public static bool operator !=(GuidId left, GuidId right) => !left.Equals(right);

    public static bool operator <(GuidId left, GuidId right) => left.CompareTo(right) < 0;

    public static bool operator >(GuidId left, GuidId right) => left.CompareTo(right) > 0;

    public static bool operator <=(GuidId left, GuidId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(GuidId left, GuidId right) => left.CompareTo(right) >= 0;

    public static implicit operator Guid(GuidId id) => id.Value;

    public static implicit operator GuidId(Guid value) => new(value);
}

public readonly struct IntId : IStronglyTypedId<int>, IEquatable<IntId>, IComparable<IntId>
{
    public int Value { get; }

    public IntId(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Int ID must be positive.", nameof(value));
        Value = value;
    }

    public bool Equals(IntId other) => Value == other.Value;

    public int CompareTo(IntId other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj) => obj is IntId other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();

    public static bool operator ==(IntId left, IntId right) => left.Equals(right);

    public static bool operator !=(IntId left, IntId right) => !left.Equals(right);

    public static bool operator <(IntId left, IntId right) => left.CompareTo(right) < 0;

    public static bool operator >(IntId left, IntId right) => left.CompareTo(right) > 0;

    public static bool operator <=(IntId left, IntId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(IntId left, IntId right) => left.CompareTo(right) >= 0;

    public static implicit operator int(IntId id) => id.Value;

    public static implicit operator IntId(int value) => new(value);
}

public readonly struct LongId : IStronglyTypedId<long>, IEquatable<LongId>, IComparable<LongId>
{
    public long Value { get; }

    public LongId(long value)
    {
        if (value <= 0)
            throw new ArgumentException("Long ID must be positive.", nameof(value));
        Value = value;
    }

    public bool Equals(LongId other) => Value == other.Value;

    public int CompareTo(LongId other) => Value.CompareTo(other.Value);

    public override bool Equals(object? obj) => obj is LongId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(LongId left, LongId right) => left.Equals(right);

    public static bool operator !=(LongId left, LongId right) => !left.Equals(right);

    public static bool operator <(LongId left, LongId right) => left.CompareTo(right) < 0;

    public static bool operator >(LongId left, LongId right) => left.CompareTo(right) > 0;

    public static bool operator <=(LongId left, LongId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(LongId left, LongId right) => left.CompareTo(right) >= 0;

    public static implicit operator long(LongId id) => id.Value;

    public static implicit operator LongId(long value) => new(value);
}

public readonly struct StringId : IStronglyTypedId<string>, IEquatable<StringId>, IComparable<StringId>
{
    public string Value { get; }

    public StringId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("String ID must not be null or whitespace.", nameof(value));
        Value = value;
    }

    public bool Equals(StringId other) => Value == other.Value;

    public int CompareTo(StringId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is StringId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(StringId left, StringId right) => left.Equals(right);

    public static bool operator !=(StringId left, StringId right) => !left.Equals(right);

    public static bool operator <(StringId left, StringId right) => left.CompareTo(right) < 0;

    public static bool operator >(StringId left, StringId right) => left.CompareTo(right) > 0;

    public static bool operator <=(StringId left, StringId right) => left.CompareTo(right) <= 0;

    public static bool operator >=(StringId left, StringId right) => left.CompareTo(right) >= 0;

    public static implicit operator string(StringId id) => id.Value;

    public static implicit operator StringId(string value) => new(value);
}
