namespace Phantom.Messaging.Abstractions;

public interface IMessageSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T message);
    T Deserialize<T>(byte[] data);
}
