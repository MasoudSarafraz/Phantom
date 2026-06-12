using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Phantom.Tests.Messaging;

// ─── Test Integration Event ─────────────────────────────────────

public class TestOrderCreatedEvent : IntegrationEvent
{
    public string OrderId { get; }
    public TestOrderCreatedEvent(string orderId) { OrderId = orderId; }
}

public class TestPaymentProcessedEvent : IntegrationEvent
{
    public decimal Amount { get; }
    public TestPaymentProcessedEvent(decimal amount) { Amount = amount; }
}

// ─── Test Handlers ──────────────────────────────────────────────

public class TestOrderCreatedHandler : IIntegrationEventHandler<TestOrderCreatedEvent>
{
    public static bool WasCalled = false;
    public static string? LastOrderId = null;

    public Task HandleAsync(TestOrderCreatedEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        LastOrderId = integrationEvent.OrderId;
        return Task.CompletedTask;
    }
}

public class TestPaymentHandler : IIntegrationEventHandler<TestPaymentProcessedEvent>
{
    public static bool WasCalled = false;

    public Task HandleAsync(TestPaymentProcessedEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}

// ─── ChannelRegistry Tests ──────────────────────────────────────

public class ChannelRegistryTests
{
    private IChannelRegistry CreateRegistry()
    {
        var logger = new LoggerFactory().CreateLogger<ChannelRegistry>();
        return new ChannelRegistry(logger);
    }

    private IChannelAdapter CreateMockAdapter(string name)
    {
        return new MockChannelAdapter(name);
    }

    [Fact]
    public void Register_Should_Add_Adapter()
    {
        var registry = CreateRegistry();
        var adapter = CreateMockAdapter("orders");

        registry.Register("orders", adapter);

        var channels = registry.GetChannels("orders");
        Assert.Single(channels);
    }

    [Fact]
    public void GetChannels_For_NonExistent_Name_Should_Return_Empty()
    {
        var registry = CreateRegistry();

        var channels = registry.GetChannels("nonexistent");
        Assert.Empty(channels);
    }

    [Fact]
    public void MapEventToChannel_Should_Create_Mapping()
    {
        var registry = CreateRegistry();
        registry.Register("orders", CreateMockAdapter("orders"));
        registry.MapEventToChannel<TestOrderCreatedEvent>("orders");

        var channels = registry.GetChannelsForEvent<TestOrderCreatedEvent>();
        Assert.Single(channels);
    }

    [Fact]
    public void GetChannelsForEvent_Without_Mapping_Should_Return_Empty()
    {
        var registry = CreateRegistry();
        registry.Register("orders", CreateMockAdapter("orders"));

        // CRITICAL FIX TEST: should NOT broadcast to all channels
        var channels = registry.GetChannelsForEvent<TestOrderCreatedEvent>();
        Assert.Empty(channels);
    }

    [Fact]
    public void MapEventToChannel_With_Null_Name_Should_Throw()
    {
        var registry = CreateRegistry();
        Assert.Throws<ArgumentException>(() => registry.MapEventToChannel<TestOrderCreatedEvent>(""));
    }

    [Fact]
    public void Register_With_Null_Name_Should_Throw()
    {
        var registry = CreateRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register("", CreateMockAdapter("x")));
    }

    [Fact]
    public void Register_With_Null_Adapter_Should_Throw()
    {
        var registry = CreateRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register("ch", null!));
    }

    [Fact]
    public void MapEventToChannels_Should_Map_To_Multiple()
    {
        var registry = CreateRegistry();
        registry.Register("orders", CreateMockAdapter("orders"));
        registry.Register("notifications", CreateMockAdapter("notifications"));
        registry.MapEventToChannels<TestOrderCreatedEvent>("orders", "notifications");

        var channels = registry.GetChannelsForEvent<TestOrderCreatedEvent>();
        Assert.Equal(2, channels.Count);
    }

    [Fact]
    public void GetAllAdapters_Should_Return_All()
    {
        var registry = CreateRegistry();
        registry.Register("ch1", CreateMockAdapter("ch1"));
        registry.Register("ch2", CreateMockAdapter("ch2"));

        var allAdapters = registry.GetAllAdapters();
        Assert.Equal(2, allAdapters.Count);
    }

    [Fact]
    public void GetAllChannelNames_Should_Return_All_Names()
    {
        var registry = CreateRegistry();
        registry.Register("ch1", CreateMockAdapter("ch1"));
        registry.Register("ch2", CreateMockAdapter("ch2"));

        var names = registry.GetAllChannelNames();
        Assert.Equal(2, names.Count);
        Assert.Contains("ch1", names);
        Assert.Contains("ch2", names);
    }

    [Fact]
    public void MapEventToChannel_With_Type_Should_Work()
    {
        var registry = CreateRegistry();
        registry.Register("orders", CreateMockAdapter("orders"));
        registry.MapEventToChannel(typeof(TestOrderCreatedEvent), "orders");

        var channels = registry.GetChannelsForEvent<TestOrderCreatedEvent>();
        Assert.Single(channels);
    }
}

// ─── InMemory Channel Tests ─────────────────────────────────────

public class InMemoryChannelTests
{
    [Fact]
    public async Task InMemory_Publish_With_Subscriptions_Should_Invoke_Handler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IIntegrationEventHandler<TestOrderCreatedEvent>, TestOrderCreatedHandler>();
        services.AddTransient<TestOrderCreatedHandler>();
        var sp = services.BuildServiceProvider();

        var logger = new LoggerFactory().CreateLogger<InMemoryChannelAdapter>();
        var adapter = new InMemoryChannelAdapter("test-channel", sp, logger);
        adapter.Subscribe<TestOrderCreatedEvent, TestOrderCreatedHandler>();

        TestOrderCreatedHandler.WasCalled = false;
        await adapter.PublishAsync(new TestOrderCreatedEvent("ORD-123"));

        Assert.True(TestOrderCreatedHandler.WasCalled);
    }

    [Fact]
    public async Task InMemory_Publish_Without_Subscriptions_Should_Not_Throw()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = new LoggerFactory().CreateLogger<InMemoryChannelAdapter>();
        var adapter = new InMemoryChannelAdapter("test-channel", sp, logger);

        // Should not throw
        await adapter.PublishAsync(new TestOrderCreatedEvent("ORD-123"));
    }

    [Fact]
    public void InMemory_IsStarted_Should_Be_True_After_Start()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = new LoggerFactory().CreateLogger<InMemoryChannelAdapter>();
        var adapter = new InMemoryChannelAdapter("test-channel", sp, logger);

        Assert.False(adapter.IsStarted);
        adapter.StartAsync().Wait();
        Assert.True(adapter.IsStarted);

        adapter.StopAsync().Wait();
        Assert.False(adapter.IsStarted);
    }
}

// ─── EventPublisher Tests ───────────────────────────────────────

public class EventPublisherTests
{
    [Fact]
    public async Task PublishAsync_By_Channel_Should_Publish_To_Channel()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IIntegrationEventHandler<TestOrderCreatedEvent>, TestOrderCreatedHandler>();
        services.AddTransient<TestOrderCreatedHandler>();
        var sp = services.BuildServiceProvider();

        var adapter = new InMemoryChannelAdapter("orders", sp, new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        adapter.Subscribe<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register("orders", adapter);

        var publisher = new EventPublisher(registry, new LoggerFactory().CreateLogger<EventPublisher>());

        TestOrderCreatedHandler.WasCalled = false;
        await publisher.PublishAsync(new TestOrderCreatedEvent("ORD-1"), "orders");

        Assert.True(TestOrderCreatedHandler.WasCalled);
    }

    [Fact]
    public async Task PublishAsync_By_EventType_Should_Use_Mapping()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IIntegrationEventHandler<TestOrderCreatedEvent>, TestOrderCreatedHandler>();
        services.AddTransient<TestOrderCreatedHandler>();
        var sp = services.BuildServiceProvider();

        var adapter = new InMemoryChannelAdapter("orders", sp, new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        adapter.Subscribe<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register("orders", adapter);
        registry.MapEventToChannel<TestOrderCreatedEvent>("orders");

        var publisher = new EventPublisher(registry, new LoggerFactory().CreateLogger<EventPublisher>());

        TestOrderCreatedHandler.WasCalled = false;
        await publisher.PublishAsync(new TestOrderCreatedEvent("ORD-2"));

        Assert.True(TestOrderCreatedHandler.WasCalled);
    }
}

// ─── PhantomMessagingOptions Tests ──────────────────────────────

public class PhantomMessagingOptionsTests
{
    [Fact]
    public void AddChannel_Should_Add_ChannelBuilder()
    {
        var options = new PhantomMessagingOptions();
        options.AddChannel("default", c => c.UseInMemory());

        Assert.Single(options.ChannelBuilders);
        Assert.True(options.ChannelBuilders.ContainsKey("default"));
    }

    [Fact]
    public void ConfigureRetry_Should_Store_Options()
    {
        var options = new PhantomMessagingOptions();
        options.ConfigureRetry(5, TimeSpan.FromSeconds(2));

        Assert.NotNull(options.Retry);
        Assert.Equal(5, options.Retry.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Retry.BaseDelay);
    }

    [Fact]
    public void ConfigureCircuitBreaker_Should_Store_Options()
    {
        var options = new PhantomMessagingOptions();
        options.ConfigureCircuitBreaker(10, TimeSpan.FromSeconds(30));

        Assert.NotNull(options.CircuitBreaker);
        Assert.Equal(10, options.CircuitBreaker.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), options.CircuitBreaker.ResetTimeout);
    }

    [Fact]
    public void UseOutboxProcessing_Should_Enable_Outbox()
    {
        var options = new PhantomMessagingOptions();
        options.UseOutboxProcessing(50, TimeSpan.FromSeconds(10));

        Assert.True(options.UseOutbox);
        Assert.Equal(50, options.OutboxBatchSize);
        Assert.Equal(TimeSpan.FromSeconds(10), options.OutboxPollingInterval);
    }

    [Fact]
    public void EnableIdempotency_Should_Set_Flag()
    {
        var options = new PhantomMessagingOptions();
        options.EnableIdempotency();

        Assert.True(options.UseIdempotency);
    }
}

// ─── OutboxMessage Tests ────────────────────────────────────────

public class OutboxMessageTests
{
    [Fact]
    public void OutboxMessage_Default_Values()
    {
        var msg = new OutboxMessage();

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(OutboxMessage.DefaultChannel, msg.Channel);
        Assert.False(msg.IsPublished);
        Assert.Equal(0, msg.RetryCount);
    }
}

// ─── IMessageSerializer Tests ───────────────────────────────────

public class MessageSerializerTests
{
    [Fact]
    public void JsonMessageSerializer_Should_Serialize_And_Deserialize()
    {
        var serializer = new JsonMessageSerializer();
        var evt = new TestOrderCreatedEvent("ORD-123");

        var bytes = serializer.Serialize(evt);
        Assert.NotEmpty(bytes);

        var deserialized = serializer.Deserialize<TestOrderCreatedEvent>(bytes);
        Assert.Equal("ORD-123", deserialized.OrderId);
    }

    [Fact]
    public void JsonMessageSerializer_Should_Deserialize_By_Type()
    {
        var serializer = new JsonMessageSerializer();
        var evt = new TestOrderCreatedEvent("ORD-456");

        var bytes = serializer.Serialize(evt);
        var deserialized = serializer.Deserialize(bytes, typeof(TestOrderCreatedEvent)) as TestOrderCreatedEvent;

        Assert.NotNull(deserialized);
        Assert.Equal("ORD-456", deserialized.OrderId);
    }

    [Fact]
    public void JsonMessageSerializer_Content_Type_Should_Be_Json()
    {
        var serializer = new JsonMessageSerializer();
        Assert.Equal("application/json", serializer.ContentType);
    }

    [Fact]
    public void JsonMessageSerializer_TryDeserialize_Should_Work()
    {
        var serializer = new JsonMessageSerializer();
        var evt = new TestOrderCreatedEvent("ORD-789");
        var bytes = serializer.Serialize(evt);

        var (success, value) = serializer.TryDeserialize<TestOrderCreatedEvent>(bytes);
        Assert.True(success);
        Assert.NotNull(value);
        Assert.Equal("ORD-789", value.OrderId);
    }
}

// ─── Helper ─────────────────────────────────────────────────────

internal class MockChannelAdapter : IChannelAdapter
{
    public string ChannelName { get; }
    public bool IsStarted => _isStarted;
    private bool _isStarted;

    public MockChannelAdapter(string name) { ChannelName = name; }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
        => Task.CompletedTask;

    public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }

    public Task StartAsync(CancellationToken ct = default) { _isStarted = true; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { _isStarted = false; return Task.CompletedTask; }
}
