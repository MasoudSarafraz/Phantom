using System.Text.Json;
using System.Text.Json.Serialization;

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

    public byte[] Serialize<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, _options);
    public T Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, _options)!;
}
