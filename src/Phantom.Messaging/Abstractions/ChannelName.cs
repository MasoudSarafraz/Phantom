using System.Diagnostics.CodeAnalysis;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Strongly-typed wrapper around a channel name. Replaces the magic-string pattern
/// (<c>AddChannel("orders", ...)</c>, <c>RouteEvent&lt;T&gt;("orders")</c>, <c>publisher.PublishAsync(evt, "orders")</c>)
/// with a value type that can be passed around safely.
///
/// A <see cref="ChannelName"/> can be constructed from a string literal but is then a
/// distinct type — typos at one call site cannot silently send an event to a different
/// channel at another call site. <see cref="Channels"/> provides a place to declare your
/// application's channel names as constants.
/// </summary>
public readonly struct ChannelName : IEquatable<ChannelName>
{
    public string Value { get; }

    private ChannelName(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a <see cref="ChannelName"/> from a string. Throws if the name is empty
    /// or whitespace — channel names are identifiers and cannot be blank.
    /// </summary>
    public static ChannelName From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Channel name must not be null, empty, or whitespace.", nameof(value));
        return new ChannelName(value);
    }

    /// <summary>
    /// Implicit conversion from a non-null, non-empty string literal to a ChannelName.
    /// This lets you write <c>AddChannel(ChannelName.From("orders"), ...)</c> or, when
    /// the call site already declares a typed ChannelName, simply pass it around without
    /// a wrapper call.
    /// </summary>
    public static implicit operator ChannelName(string value) => From(value);

    /// <summary>
    /// Implicit conversion back to string so ChannelName can be used wherever the legacy
    /// string-based API is still being called.
    /// </summary>
    public static implicit operator string(ChannelName channel) => channel.Value;

    public bool Equals(ChannelName other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ChannelName other && Equals(other);

    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(ChannelName left, ChannelName right) => left.Equals(right);
    public static bool operator !=(ChannelName left, ChannelName right) => !left.Equals(right);

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Optional base class for declaring your application's channel names as constants in
/// one place. Usage:
/// <code>
/// public static class Channels {
///     public static readonly ChannelName Orders        = ChannelName.From("orders");
///     public static readonly ChannelName Notifications = ChannelName.From("notifications");
/// }
/// </code>
/// </summary>
public static class Channels
{
    /// <summary>
    /// The conventional name used for the default channel when none is specified.
    /// </summary>
    public static readonly ChannelName Default = ChannelName.From("default");
}
