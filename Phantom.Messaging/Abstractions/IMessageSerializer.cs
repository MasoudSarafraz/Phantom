namespace Phantom.Messaging.Abstractions;

/// <summary>
/// Defines a serializer for converting messages to and from byte arrays
/// for transport over messaging infrastructure.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Gets the content type identifier for this serializer (e.g., "application/json").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a message to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the message to serialize.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>A byte array representation of the message.</returns>
    byte[] Serialize<T>(T message);

    /// <summary>
    /// Deserializes a byte array to a message of the specified generic type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the message as.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized message.</returns>
    T Deserialize<T>(byte[] data);

    /// <summary>
    /// Deserializes a byte array to a message of the specified type.
    /// Useful when the type is not known at compile time, such as during message routing.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <param name="type">The runtime type to deserialize the message as.</param>
    /// <returns>The deserialized message object.</returns>
    object Deserialize(byte[] data, Type type);

    /// <summary>
    /// Attempts to deserialize a byte array to a message of the specified generic type.
    /// Returns a tuple indicating success and the deserialized value.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the message as.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>
    /// A tuple where <c>Success</c> is <c>true</c> if deserialization succeeded,
    /// and <c>Value</c> contains the deserialized message (or <c>default</c> on failure).
    /// </returns>
    (bool Success, T? Value) TryDeserialize<T>(byte[] data);
}
