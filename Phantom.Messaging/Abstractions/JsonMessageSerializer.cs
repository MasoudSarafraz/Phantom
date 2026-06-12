using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phantom.Messaging.Abstractions;

/// <summary>
/// JSON-based implementation of <see cref="IMessageSerializer"/> using
/// <see cref="System.Text.Json"/>. Supports customizable serializer options
/// and provides robust error handling for deserialization operations.
/// </summary>
public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageSerializer"/> class
    /// with default serializer options (camelCase naming, null omission, string enums).
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageSerializer"/> class
    /// with custom serializer options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for all serialization operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, _options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(byte[] data)
    {
        var result = JsonSerializer.Deserialize<T>(data, _options);
        if (result is null)
            throw new JsonException($"Failed to deserialize {typeof(T).Name}: result was null.");

        return result;
    }

    /// <inheritdoc />
    public object Deserialize(byte[] data, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var result = JsonSerializer.Deserialize(data, type, _options)
            ?? throw new JsonException($"Failed to deserialize {type.Name}: result was null.");

        return result;
    }

    /// <inheritdoc />
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
