namespace Phantom.Core.Services;

/// <summary>
/// Serializes and deserializes messages for the outbox pattern.
/// When registered, domain events are serialized into OutboxMessage rows
/// instead of being dispatched directly.
/// </summary>
public interface IMessageSerializer
{
    string ContentType { get; }

    byte[] Serialize<T>(T message);

    T Deserialize<T>(byte[] data);

    object Deserialize(byte[] data, Type type);

    (bool Success, T? Value) TryDeserialize<T>(byte[] data);
}
