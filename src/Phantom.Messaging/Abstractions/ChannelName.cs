using System.Diagnostics.CodeAnalysis;

namespace Phantom.Messaging.Abstractions;

public readonly struct ChannelName : IEquatable<ChannelName>
{
    public string Value { get; }

    private ChannelName(string value)
    {
        Value = value;
    }

    public static ChannelName From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Channel name must not be null, empty, or whitespace.", nameof(value));
        return new ChannelName(value);
    }

    public static implicit operator ChannelName(string value) => From(value);

    public static implicit operator string(ChannelName channel) => channel.Value;

    public bool Equals(ChannelName other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ChannelName other && Equals(other);

    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(ChannelName left, ChannelName right) => left.Equals(right);
    public static bool operator !=(ChannelName left, ChannelName right) => !left.Equals(right);

    public override string ToString() => Value ?? string.Empty;
}

public static class Channels
{

    public static readonly ChannelName Default = ChannelName.From("default");
}
