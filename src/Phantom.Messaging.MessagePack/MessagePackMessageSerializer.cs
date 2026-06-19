using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Phantom.Core.Services;
using Phantom.Messaging.Abstractions;

namespace Phantom.Messaging.MessagePack;

/// <summary>
/// <see cref="IMessageSerializer"/> implementation backed by MessagePack.
///
/// MessagePack produces compact binary payloads (typically 3-5x smaller than JSON for the
/// same data) and serializes faster than System.Text.Json. Use this when throughput or
/// payload size matter more than human readability — e.g. high-volume event streams between
/// microservices, or RabbitMQ deployments where message size affects network cost.
///
/// Type resolution is performed by MessagePack's <see cref="TypelessContractlessStandardResolver"/>
/// so that no [MessagePackObject] attributes are required on integration event classes.
/// </summary>
public class MessagePackMessageSerializer : IMessageSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public MessagePackMessageSerializer(MessagePackSerializerOptions? options = null)
    {
        // Default to the TypelessContractlessStandardResolver so that any POCO can be
        // serialized without [MessagePackObject] attributes. This matches the way
        // JsonMessageSerializer works (no attributes required on event classes).
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
    /// <summary>
    /// Replaces the default <see cref="IMessageSerializer"/> registration with a
    /// <see cref="MessagePackMessageSerializer"/>. Call this AFTER AddPhantom() so the
    /// registration overrides the default JsonMessageSerializer that Phantom registers.
    /// </summary>
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
