using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.Core.Services;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.MessagePack;

public class MessagePackMessageSerializer : IMessageSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public MessagePackMessageSerializer(MessagePackSerializerOptions? options = null)
    {

        _options = options ?? TypelessContractlessStandardResolver.Options;
    }

    public string ContentType => "application/x-msgpack";

    public byte[] Serialize<T>(T message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return MessagePackSerializer.Typeless.Serialize(message, _options);
    }

    public T Deserialize<T>(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return (T)MessagePackSerializer.Typeless.Deserialize(data, _options)!;
    }

    public object Deserialize(byte[] data, Type type)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(type);
        return MessagePackSerializer.Typeless.Deserialize(data, _options)!;
    }

    public (bool Success, T? Value) TryDeserialize<T>(byte[] data)
    {
        try
        {
            var value = Deserialize<T>(data);
            return (true, value);
        }
        catch
        {
            return (false, default);
        }
    }
}

public static class MessagePackServiceCollectionExtensions
{

    public static IServiceCollection UseMessagePackSerializer(
        this IServiceCollection services,
        MessagePackSerializerOptions? options = null)
    {
        services.Replace(new ServiceDescriptor(
            typeof(IMessageSerializer),
            _ => new MessagePackMessageSerializer(options),
            ServiceLifetime.Singleton));
        return services;
    }
}
