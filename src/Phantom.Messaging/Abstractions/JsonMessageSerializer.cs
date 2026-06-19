using System.Text.Json;
using System.Text.Json.Serialization;
using Phantom.Core.Services;

namespace Phantom.Messaging.Abstractions;

public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json";

    public JsonMessageSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public byte[] Serialize<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    public T Deserialize<T>(byte[] data)
    {
        var result = JsonSerializer.Deserialize<T>(data, _options);
        if (result is null)
            throw new JsonException($"Failed to deserialize {typeof(T).Name}: result was null.");

        return result;
    }

    public object Deserialize(byte[] data, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var result = JsonSerializer.Deserialize(data, type, _options)
            ?? throw new JsonException($"Failed to deserialize {type.Name}: result was null.");

        return result;
    }

    public (bool Success, T? Value) TryDeserialize<T>(byte[] data)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(data, _options);
            return (result is not null, result);
        }
        catch (JsonException)
        {
            return (false, default);
        }
    }
}
