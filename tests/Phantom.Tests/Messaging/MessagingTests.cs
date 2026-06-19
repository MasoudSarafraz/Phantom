using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Polly.Retry;

namespace Phantom.Tests.Messaging;


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

        await adapter.PublishAsync(new TestOrderCreatedEvent("ORD-123"));
    }

    [Fact]
    public async Task InMemory_IsStarted_Should_Be_True_After_Start()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var logger = new LoggerFactory().CreateLogger<InMemoryChannelAdapter>();
        var adapter = new InMemoryChannelAdapter("test-channel", sp, logger);

        Assert.False(adapter.IsStarted);
        await adapter.StartAsync();
        Assert.True(adapter.IsStarted);

        await adapter.StopAsync();
        Assert.False(adapter.IsStarted);
    }
}


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

        var publisher = new EventPublisher(registry, NullResiliencePipeline.Instance, new LoggerFactory().CreateLogger<EventPublisher>());

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

        var publisher = new EventPublisher(registry, NullResiliencePipeline.Instance, new LoggerFactory().CreateLogger<EventPublisher>());

        TestOrderCreatedHandler.WasCalled = false;
        await publisher.PublishAsync(new TestOrderCreatedEvent("ORD-2"));

        Assert.True(TestOrderCreatedHandler.WasCalled);
    }
}


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
    public void ChannelName_From_String_Should_Be_Strongly_Typed()
    {
        ChannelName name = ChannelName.From("orders");
        Assert.Equal("orders", name.Value);
        Assert.Equal("orders", name); // implicit conversion to string
    }

    [Fact]
    public void ChannelName_From_Empty_String_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => ChannelName.From(""));
        Assert.Throws<ArgumentException>(() => ChannelName.From("   "));
        Assert.Throws<ArgumentException>(() => ChannelName.From(null!));
    }

    [Fact]
    public void ChannelName_Should_Equal_By_Value()
    {
        var a = ChannelName.From("orders");
        var b = ChannelName.From("orders");
        var c = ChannelName.From("notifications");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ChannelName_Implicit_From_String_Should_Work()
    {
        // The implicit conversion allows declaring constants as:
        //   public static readonly ChannelName Orders = "orders";
        ChannelName name = "orders";
        Assert.Equal("orders", name.Value);
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


public class ResiliencePipelineTests
{
    [Fact]
    public async Task NullResiliencePipeline_Should_Execute_Action_Once()
    {
        var calls = 0;
        await NullResiliencePipeline.Instance.ExecuteAsync(async ct =>
        {
            await Task.Yield();
            calls++;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CompositeResiliencePipeline_Should_Retry_On_Failure()
    {
        // Retry 3 times with a tiny base delay so the test stays fast.
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(1),
            BackoffType = DelayBackoffType.Constant,
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>()
        };

        var pipeline = new CompositeResiliencePipeline(retry);

        var attempts = 0;
        await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Yield();
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("transient");
        });

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CompositeResiliencePipeline_Should_Propagate_After_Exhausting_Retries()
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(1),
            BackoffType = DelayBackoffType.Constant,
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>()
        };

        var pipeline = new CompositeResiliencePipeline(retry);

        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                attempts++;
                throw new InvalidOperationException("always fails");
            });
        });

        // 1 initial attempt + 2 retries = 3 total invocations.
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task EventPublisher_Should_Retry_Transient_Adapter_Failure()
    {
        // Arrange: a fake adapter that fails twice then succeeds.
        // The publisher must invoke the resilience pipeline so that the third call
        // finally publishes the event.
        var mockAdapter = new Moq.Mock<IChannelAdapter>();
        mockAdapter.SetupGet(a => a.ChannelName).Returns("orders");
        var callCount = 0;
        mockAdapter.Setup(a => a.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns((IIntegrationEvent e, CancellationToken ct) =>
            {
                callCount++;
                if (callCount < 3)
                    throw new InvalidOperationException("broker down");
                return Task.CompletedTask;
            });

        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        registry.Register("orders", mockAdapter.Object);

        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(1),
            BackoffType = DelayBackoffType.Constant,
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>()
        };
        var pipeline = new CompositeResiliencePipeline(retry);

        var publisher = new EventPublisher(registry, pipeline, new LoggerFactory().CreateLogger<EventPublisher>());

        // Act
        await publisher.PublishAsync(new TestOrderCreatedEvent("ORD-RETRY"), "orders");

        // Assert
        Assert.Equal(3, callCount);
    }
}


public class OutboxProcessorTests
{
    /// <summary>
    /// The OutboxProcessor should deserialize pending outbox messages, publish them via
    /// IEventPublisher, and call MarkAsPublishedAsync on the repository when publishing
    /// succeeds.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_Should_Publish_Pending_Messages_And_Mark_As_Published()
    {
        // Arrange — fake repository with one pending message
        var evt = new TestOrderCreatedEvent("ORD-OUTBOX-1");
        var serializer = new JsonMessageSerializer();
        var payload = System.Text.Encoding.UTF8.GetString(serializer.Serialize(evt));

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TestOrderCreatedEvent).AssemblyQualifiedName!,
            Payload = payload,
            IsPublished = false,
            Channel = OutboxMessage.DefaultChannel
        };

        var repo = new Moq.Mock<IOutboxMessageRepository>();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });

        // Capture the published event so we can assert on its contents below.
        // The OutboxProcessor calls PublishAsync<IIntegrationEvent> because the deserialized
        // event is typed as IIntegrationEvent at the call site, so the Setup must match that
        // generic instantiation.
        IIntegrationEvent? publishedEvent = null;
        var publisher = new Moq.Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
                 .Callback<IIntegrationEvent, CancellationToken>((e, _) => publishedEvent = e)
                 .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(publisher.Object);
        var sp = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            sp,
            serializer,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<OutboxProcessor>(),
            batchSize: 10,
            pollingInterval: TimeSpan.FromMilliseconds(100));

        // Act — invoke ProcessAsync once (does NOT start the background loop)
        await processor.ProcessAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal("ORD-OUTBOX-1", ((TestOrderCreatedEvent)publishedEvent!).OrderId);
        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.MarkAsPublishedAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.IncrementRetryCountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "IncrementRetryCountAsync must not be called when publishing succeeded.");
    }

    /// <summary>
    /// When publishing fails (even after the resilience pipeline exhausts its retries),
    /// the OutboxProcessor should call IncrementRetryCountAsync so the message can be
    /// retried in a later sweep. The message must NOT be marked as published.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_Should_Increment_Retry_Count_When_Publish_Fails()
    {
        var evt = new TestOrderCreatedEvent("ORD-OUTBOX-FAIL");
        var serializer = new JsonMessageSerializer();
        var payload = System.Text.Encoding.UTF8.GetString(serializer.Serialize(evt));

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TestOrderCreatedEvent).AssemblyQualifiedName!,
            Payload = payload,
            IsPublished = false,
            Channel = OutboxMessage.DefaultChannel
        };

        var repo = new Moq.Mock<IOutboxMessageRepository>();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });

        var publisher = new Moq.Mock<IEventPublisher>();
        publisher.Setup(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(publisher.Object);
        var sp = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            sp,
            serializer,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<OutboxProcessor>(),
            batchSize: 10,
            pollingInterval: TimeSpan.FromMilliseconds(100));

        // Act — must NOT throw; OutboxProcessor swallows per-message exceptions
        await processor.ProcessAsync(CancellationToken.None);

        // Assert
        repo.Verify(r => r.MarkAsPublishedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "MarkAsPublishedAsync must not be called when publishing failed.");
        repo.Verify(r => r.IncrementRetryCountAsync(message.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "IncrementRetryCountAsync must be called so the message is retried later.");
    }

    /// <summary>
    /// When the repository returns an empty list, the processor should not attempt any
    /// publish or update calls. This is the common idle case.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_With_No_Pending_Messages_Should_Do_Nothing()
    {
        var repo = new Moq.Mock<IOutboxMessageRepository>();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage>());

        var publisher = new Moq.Mock<IEventPublisher>();
        var serializer = new JsonMessageSerializer();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(publisher.Object);
        var sp = services.BuildServiceProvider();

        var processor = new OutboxProcessor(
            sp, serializer, NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<OutboxProcessor>(),
            batchSize: 10, pollingInterval: TimeSpan.FromMilliseconds(100));

        await processor.ProcessAsync(CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.MarkAsPublishedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.IncrementRetryCountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}


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
    public void MessagePackMessageSerializer_Should_Serialize_And_Deserialize()
    {
        var serializer = new Phantom.Messaging.MessagePack.MessagePackMessageSerializer();
        var evt = new TestOrderCreatedEvent("ORD-MP-1");

        var bytes = serializer.Serialize(evt);
        Assert.NotEmpty(bytes);

        var deserialized = serializer.Deserialize<TestOrderCreatedEvent>(bytes);
        Assert.Equal("ORD-MP-1", deserialized.OrderId);
    }

    [Fact]
    public void MessagePackMessageSerializer_Should_Produce_Smaller_Payload_Than_Json()
    {
        // For most realistic event shapes, MessagePack produces a more compact binary
        // representation than JSON. We assert this on a representative event so that a
        // regression in MessagePack configuration is caught.
        var jsonSerializer = new JsonMessageSerializer();
        var mpSerializer = new Phantom.Messaging.MessagePack.MessagePackMessageSerializer();
        var evt = new TestOrderCreatedEvent("ORD-CMP");

        var jsonBytes = jsonSerializer.Serialize(evt);
        var mpBytes = mpSerializer.Serialize(evt);

        Assert.True(mpBytes.Length < jsonBytes.Length,
            $"Expected MessagePack ({mpBytes.Length} bytes) to be smaller than JSON ({jsonBytes.Length} bytes) for the same event.");
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


public class IdempotentIntegrationEventHandlerDecoratorTests
{
    [Fact]
    public async Task Should_Call_Inner_Handler_When_Event_Not_Processed()
    {
        // Arrange
        var trackerMock = new Mock<IIdempotencyTracker>();
        trackerMock.Setup(t => t.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var innerHandlerMock = new Mock<IIntegrationEventHandler<TestOrderCreatedEvent>>();
        innerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<TestOrderCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>>();

        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>(
            innerHandlerMock.Object, trackerMock.Object, logger);

        var evt = new TestOrderCreatedEvent("ORD-1");

        // Act
        await decorator.HandleAsync(evt);

        // Assert
        innerHandlerMock.Verify(h => h.HandleAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
        trackerMock.Verify(t => t.MarkAsProcessedAsync(evt.EventId, evt.EventName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_Skip_Handler_When_Event_Already_Processed()
    {
        // Arrange
        var trackerMock = new Mock<IIdempotencyTracker>();
        trackerMock.Setup(t => t.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var innerHandlerMock = new Mock<IIntegrationEventHandler<TestOrderCreatedEvent>>();

        var logger = new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>>();

        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>(
            innerHandlerMock.Object, trackerMock.Object, logger);

        var evt = new TestOrderCreatedEvent("ORD-2");

        // Act
        await decorator.HandleAsync(evt);

        // Assert
        innerHandlerMock.Verify(h => h.HandleAsync(It.IsAny<TestOrderCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        trackerMock.Verify(t => t.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_Throw_When_Event_Is_Null()
    {
        var trackerMock = new Mock<IIdempotencyTracker>();
        var innerHandlerMock = new Mock<IIntegrationEventHandler<TestOrderCreatedEvent>>();
        var logger = new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>>();

        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>(
            innerHandlerMock.Object, trackerMock.Object, logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => decorator.HandleAsync(null!));
    }

    [Fact]
    public async Task Should_Not_Mark_As_Processed_When_Inner_Handler_Throws()
    {
        // Arrange
        var trackerMock = new Mock<IIdempotencyTracker>();
        trackerMock.Setup(t => t.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var innerHandlerMock = new Mock<IIntegrationEventHandler<TestOrderCreatedEvent>>();
        innerHandlerMock.Setup(h => h.HandleAsync(It.IsAny<TestOrderCreatedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        var logger = new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>>();

        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestOrderCreatedEvent>(
            innerHandlerMock.Object, trackerMock.Object, logger);

        var evt = new TestOrderCreatedEvent("ORD-3");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.HandleAsync(evt));
        trackerMock.Verify(t => t.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}


public class OutboxDefaultEnabledTests
{
    [Fact]
    public void PhantomDataOptions_UseOutbox_Should_Default_To_True()
    {
        var options = new Phantom.Data.Extensions.PhantomDataOptions();
        Assert.True(options.UseOutbox);
    }

    [Fact]
    public void PhantomMessagingOptions_UseOutbox_Should_Default_To_True()
    {
        var options = new PhantomMessagingOptions();
        Assert.True(options.UseOutbox);
    }
}
