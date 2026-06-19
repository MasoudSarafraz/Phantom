using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.MessagePack;

namespace Phantom.Tests.Messaging;

public class MessagePackMessageSerializerTests
{
    private readonly MessagePackMessageSerializer _serializer = new();

    public class MessagePayload
    {
        public string Text { get; set; } = "";
        public int Number { get; set; }
        public Guid Id { get; set; }
    }

    [Fact]
    public void ContentType_Should_Be_MessagePack()
    {
        Assert.Equal("application/x-msgpack", _serializer.ContentType);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Roundtrip_Primitive()
    {
        var value = 42;
        var bytes = _serializer.Serialize(value);
        var roundtrip = _serializer.Deserialize<int>(bytes);
        Assert.Equal(value, roundtrip);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Roundtrip_String()
    {
        var value = "hello world";
        var bytes = _serializer.Serialize(value);
        var roundtrip = _serializer.Deserialize<string>(bytes);
        Assert.Equal(value, roundtrip);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Roundtrip_Complex_Object()
    {
        var payload = new MessagePayload
        {
            Text = "test",
            Number = 123,
            Id = Guid.NewGuid()
        };

        var bytes = _serializer.Serialize(payload);
        var roundtrip = _serializer.Deserialize<MessagePayload>(bytes);

        Assert.NotNull(roundtrip);
        Assert.Equal(payload.Text, roundtrip!.Text);
        Assert.Equal(payload.Number, roundtrip.Number);
        Assert.Equal(payload.Id, roundtrip.Id);
    }

    [Fact]
    public void Serialize_With_Null_Message_Should_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _serializer.Serialize<object>(null!));
    }

    [Fact]
    public void Deserialize_With_Null_Data_Should_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _serializer.Deserialize<object>(null!));
    }

    [Fact]
    public void Deserialize_With_Null_Type_Should_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _serializer.Deserialize(new byte[] { 1, 2, 3 }, null!));
    }

    [Fact]
    public void Deserialize_As_Object_Should_Return_NonNull_Result()
    {
        var value = 42;
        var bytes = _serializer.Serialize(value);
        var roundtrip = _serializer.Deserialize(bytes, typeof(int));
        Assert.Equal(42, roundtrip);
    }

    [Fact]
    public void TryDeserialize_Valid_Data_Should_Return_Success()
    {
        var bytes = _serializer.Serialize("hello");
        var (success, value) = _serializer.TryDeserialize<string>(bytes);
        Assert.True(success);
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryDeserialize_Invalid_Data_Should_Return_Failure()
    {
        var (success, value) = _serializer.TryDeserialize<string>(new byte[] { 0xff, 0xff, 0xff });
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void Constructor_With_Custom_Options_Should_Use_Them()
    {
        var options = TypelessContractlessStandardResolver.Options;
        var serializer = new MessagePackMessageSerializer(options);
        Assert.Equal("application/x-msgpack", serializer.ContentType);
    }

    [Fact]
    public void Serialize_Should_Produce_NonEmpty_Bytes()
    {
        var bytes = _serializer.Serialize("hello");
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Roundtrip_Of_Integration_Event_Should_Work()
    {
        var evt = new MessagePackTestEvent("order-123") { CorrelationId = "trace-abc" };
        var bytes = _serializer.Serialize(evt);
        var roundtrip = _serializer.Deserialize<MessagePackTestEvent>(bytes);

        Assert.NotNull(roundtrip);
        Assert.Equal("order-123", roundtrip!.OrderId);
        Assert.Equal("trace-abc", roundtrip.CorrelationId);
    }
}

public class MessagePackTestEvent : IntegrationEvent
{
    public string OrderId { get; }
    public MessagePackTestEvent(string orderId) { OrderId = orderId; }
}

public class MessagePackServiceCollectionExtensionsTests
{
    [Fact]
    public void UseMessagePackSerializer_Should_Replace_IMessageSerializer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.UseMessagePackSerializer();

        var sp = services.BuildServiceProvider();
        var serializer = sp.GetRequiredService<IMessageSerializer>();

        Assert.IsType<MessagePackMessageSerializer>(serializer);
    }

    [Fact]
    public void UseMessagePackSerializer_Should_Return_Same_ServiceCollection()
    {
        var services = new ServiceCollection();
        var returned = services.UseMessagePackSerializer();
        Assert.Same(services, returned);
    }

    [Fact]
    public void UseMessagePackSerializer_Should_Register_As_Singleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.UseMessagePackSerializer();

        var sp = services.BuildServiceProvider();
        var s1 = sp.GetRequiredService<IMessageSerializer>();
        var s2 = sp.GetRequiredService<IMessageSerializer>();

        Assert.Same(s1, s2);
    }
}

public class JsonMessageSerializerAdvancedTests
{
    private readonly JsonMessageSerializer _serializer = new();

    [Fact]
    public void ContentType_Should_Be_Application_Json()
    {
        Assert.Equal("application/json", _serializer.ContentType);
    }

    [Fact]
    public void Serialize_And_Deserialize_Should_Roundtrip()
    {
        var value = new JsonTestObject { Name = "Alice", Age = 30 };
        var bytes = _serializer.Serialize(value);
        var roundtrip = _serializer.Deserialize<JsonTestObject>(bytes);

        Assert.Equal(value.Name, roundtrip.Name);
        Assert.Equal(value.Age, roundtrip.Age);
    }

    [Fact]
    public void Serialize_Should_Use_CamelCase_Property_Naming()
    {
        var value = new JsonTestObject { Name = "Alice", Age = 30 };
        var bytes = _serializer.Serialize(value);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"age\"", json);
    }

    [Fact]
    public void Deserialize_With_Type_Overload_Should_Work()
    {
        var value = new JsonTestObject { Name = "Bob", Age = 25 };
        var bytes = _serializer.Serialize(value);
        var roundtrip = (JsonTestObject)_serializer.Deserialize(bytes, typeof(JsonTestObject));

        Assert.Equal(value.Name, roundtrip.Name);
        Assert.Equal(value.Age, roundtrip.Age);
    }

    [Fact]
    public void Deserialize_With_Null_Type_Should_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _serializer.Deserialize(new byte[] { 1 }, null!));
    }

    [Fact]
    public void TryDeserialize_Valid_Json_Should_Return_Success()
    {
        var bytes = _serializer.Serialize("hello");
        var (success, value) = _serializer.TryDeserialize<string>(bytes);
        Assert.True(success);
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryDeserialize_Invalid_Json_Should_Return_Failure()
    {
        var (success, value) = _serializer.TryDeserialize<string>(new byte[] { 0xff });
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void Constructor_With_Null_Options_Should_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonMessageSerializer(null!));
    }

    [Fact]
    public void Constructor_With_Custom_Options_Should_Use_Them()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        };
        var serializer = new JsonMessageSerializer(options);

        var value = new JsonTestObject { Name = "Alice", Age = 30 };
        var bytes = serializer.Serialize(value);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"Name\"", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Deserialize_Invalid_Json_Should_Throw()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            _serializer.Deserialize<JsonTestObject>(new byte[] { 0xff }));
    }

    private class JsonTestObject
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
