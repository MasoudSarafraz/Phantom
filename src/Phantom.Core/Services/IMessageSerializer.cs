namespace Phantom.Core.Services;

public interface IMessageSerializer
{
    string ContentType { get; }

    byte[] Serialize<T>(T message);

    T Deserialize<T>(byte[] data);

    object Deserialize(byte[] data, Type type);

    (bool Success, T? Value) TryDeserialize<T>(byte[] data);
}
