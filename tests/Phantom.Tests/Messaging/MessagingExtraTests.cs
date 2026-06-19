using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.Extensions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Polly;
using Polly.Retry;
using System.Reflection;

namespace Phantom.Tests.Messaging;

public class ChannelNameTests
{
    [Fact]
    public void From_Should_Create_With_Value()
    {
        var ch = ChannelName.From("orders");
        Assert.Equal("orders", ch.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_Should_Throw_For_Invalid_Value(string? value)
    {
        Assert.Throws<ArgumentException>(() => ChannelName.From(value!));
    }

    [Fact]
    public void Implicit_String_Operator_Should_Create_ChannelName()
    {
        ChannelName ch = "notifications";
        Assert.Equal("notifications", ch.Value);
    }

    [Fact]
    public void Implicit_ChannelName_To_String_Should_Return_Value()
    {
        var ch = ChannelName.From("audit");
        string value = ch;
        Assert.Equal("audit", value);
    }

    [Fact]
    public void Equals_Should_Be_Case_Sensitive()
    {
        var a = ChannelName.From("orders");
        var b = ChannelName.From("Orders");
        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void Equals_Should_Be_True_For_Same_Value()
    {
        var a = ChannelName.From("orders");
        var b = ChannelName.From("orders");
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_With_Null_Should_Be_False()
    {
        var a = ChannelName.From("orders");
        Assert.False(a.Equals((object?)null));
    }

    [Fact]
    public void Equals_With_Non_ChannelName_Should_Be_False()
    {
        var a = ChannelName.From("orders");
        Assert.False(a.Equals((object)"orders"));
    }

    [Fact]
    public void GetHashCode_Should_Be_Stable_For_Same_Value()
    {
        var a = ChannelName.From("orders");
        var b = ChannelName.From("orders");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_Should_Return_Value()
    {
        var ch = ChannelName.From("payments");
        Assert.Equal("payments", ch.ToString());
    }

    [Fact]
    public void Channels_Default_Should_Be_Default_String()
    {
        Assert.Equal("default", Channels.Default.Value);
    }
}

public class EventPublisherExtensionTests
{
    private sealed class StubPublisher : IEventPublisher
    {
        public (object Event, string? Channel, CancellationToken Ct)? LastCall;

        public Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent
        {
            LastCall = (@event!, channel, ct);
            return Task.CompletedTask;
        }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
        {
            LastCall = (@event!, null, ct);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_With_ChannelName_Should_Flatten_To_String()
    {
        var publisher = new StubPublisher();
        var evt = new PublisherTestEvent();
        await publisher.PublishAsync(evt, ChannelName.From("audit"), CancellationToken.None);

        Assert.NotNull(publisher.LastCall);
        Assert.Equal("audit", publisher.LastCall.Value.Channel);
        Assert.Equal(evt.EventId, ((PublisherTestEvent)publisher.LastCall.Value.Event!).EventId);
    }

    [Fact]
    public async Task PublishAsync_With_Implicit_String_To_ChannelName_Should_Work()
    {
        var publisher = new StubPublisher();
        var evt = new PublisherTestEvent();
        await publisher.PublishAsync(evt, (ChannelName)"email", CancellationToken.None);

        Assert.Equal("email", publisher.LastCall!.Value.Channel);
    }
}

public class PublisherTestEvent : IntegrationEvent
{
    public string Payload { get; }
    public PublisherTestEvent(string? payload = null) { Payload = payload ?? "x"; }
}

public class EventPublisherResilienceAndCancellationTests
{
    private sealed class CountingAdapter : IChannelAdapter
    {
        public string ChannelName { get; }
        public bool IsStarted { get; set; }
        public int PublishCount;
        public Func<Task>? OnPublish;

        public CountingAdapter(string name) { ChannelName = name; }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
        {
            PublishCount++;
            return OnPublish?.Invoke() ?? Task.CompletedTask;
        }

        public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }
        public Task StartAsync(CancellationToken ct = default) { IsStarted = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) { IsStarted = false; return Task.CompletedTask; }
    }

    [Fact]
    public async Task PublishAsync_With_Canceled_Token_Should_Short_Circuit()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new CountingAdapter("ch");
        registry.Register("ch", adapter);

        var publisher = new EventPublisher(
            registry,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<EventPublisher>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await publisher.PublishAsync(new PublisherTestEvent(), "ch", cts.Token);
        Assert.Equal(0, adapter.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_No_Channel_Overload_With_Canceled_Token_Should_Short_Circuit()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new CountingAdapter("ch");
        registry.Register("ch", adapter);
        registry.MapEventToChannel<PublisherTestEvent>("ch");

        var publisher = new EventPublisher(
            registry,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<EventPublisher>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await publisher.PublishAsync(new PublisherTestEvent(), cts.Token);
        Assert.Equal(0, adapter.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_Should_Fan_Out_To_All_Adapters_For_Channel()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var a1 = new CountingAdapter("ch");
        var a2 = new CountingAdapter("ch");
        registry.Register("ch", a1);
        registry.Register("ch", a2);

        var publisher = new EventPublisher(
            registry,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<EventPublisher>());

        await publisher.PublishAsync(new PublisherTestEvent(), "ch");

        Assert.Equal(1, a1.PublishCount);
        Assert.Equal(1, a2.PublishCount);
    }

    [Fact]
    public async Task ThrowIfNoChannelFound_True_Should_Throw_When_No_Channel_Mapped()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var publisher = new EventPublisher(
            registry,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<EventPublisher>(),
            throwIfNoChannelFound: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(new PublisherTestEvent(), "missing"));
    }

    [Fact]
    public async Task ThrowIfNoChannelFound_True_Should_Throw_When_No_Channel_For_Event_Type()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var publisher = new EventPublisher(
            registry,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<EventPublisher>(),
            throwIfNoChannelFound: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(new PublisherTestEvent()));
    }

    [Fact]
    public async Task Resilience_Pipeline_Should_Retry_Failures()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new CountingAdapter("ch");
        var attempts = 0;
        adapter.OnPublish = () =>
        {
            attempts++;
            if (attempts < 3)
                throw new InvalidOperationException("transient");
            return Task.CompletedTask;
        };
        registry.Register("ch", adapter);

        var pipeline = new CompositeResiliencePipeline(
            retry: new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(1),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new Polly.PredicateBuilder().Handle<Exception>()
            });

        var publisher = new EventPublisher(
            registry,
            pipeline,
            new LoggerFactory().CreateLogger<EventPublisher>());

        await publisher.PublishAsync(new PublisherTestEvent(), "ch");
        Assert.Equal(3, attempts);
        Assert.Equal(3, adapter.PublishCount);
    }

    [Fact]
    public async Task Resilience_Pipeline_Should_Propagate_Final_Exception()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var adapter = new CountingAdapter("ch");
        adapter.OnPublish = () => throw new InvalidOperationException("always fails");
        registry.Register("ch", adapter);

        var pipeline = new CompositeResiliencePipeline(
            retry: new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(1),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new Polly.PredicateBuilder().Handle<Exception>()
            });

        var publisher = new EventPublisher(
            registry,
            pipeline,
            new LoggerFactory().CreateLogger<EventPublisher>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(new PublisherTestEvent(), "ch"));
    }
}

public class NullResiliencePipelineTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Invoke_Action_Once()
    {
        var calls = 0;
        await NullResiliencePipeline.Instance.ExecuteAsync(_ => { calls++; return Task.CompletedTask; });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_T_Should_Return_Value()
    {
        var result = await NullResiliencePipeline.Instance.ExecuteAsync(_ => Task.FromResult(42));
        Assert.Equal(42, result);
    }

    [Fact]
    public void Instance_Should_Be_Singleton()
    {
        Assert.Same(NullResiliencePipeline.Instance, NullResiliencePipeline.Instance);
    }
}

public class ChannelAdapterHostedServiceTests
{
    private static IServiceProvider BuildMessagingProvider(Action<PhantomMessagingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomMessaging(Array.Empty<Assembly>(), configure);
        return services.BuildServiceProvider();
    }

    private static Microsoft.Extensions.Hosting.IHostedService? FindHostedService(IServiceProvider sp)
    {
        var hostedServices = sp.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        return hostedServices.FirstOrDefault(h => h.GetType().Name == "ChannelAdapterHostedService");
    }

    [Fact]
    public void AddPhantomMessaging_Should_Register_ChannelAdapterHostedService()
    {
        var sp = BuildMessagingProvider(opts =>
        {
            opts.AddChannel("a", c => c.UseInMemory());
        });

        var hostedService = FindHostedService(sp);
        Assert.NotNull(hostedService);
    }

    [Fact]
    public async Task HostedService_Should_Start_And_Stop_Without_Throwing()
    {
        var sp = BuildMessagingProvider(opts =>
        {
            opts.AddChannel("a", c => c.UseInMemory());
        });

        var hostedService = FindHostedService(sp);
        Assert.NotNull(hostedService);

        await hostedService!.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HostedService_Should_Continue_On_Adapter_Startup_Failure()
    {
        var registry = new ChannelRegistry(new LoggerFactory().CreateLogger<ChannelRegistry>());
        var throwingAdapter = new ThrowingAdapter("boom");
        var okAdapter = new InMemoryChannelAdapter("good",
            new ServiceCollection().BuildServiceProvider(),
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
        registry.Register("boom", throwingAdapter);
        registry.Register("good", okAdapter);

        var sp = BuildMessagingProvider(_ => { });
        var hostedService = FindHostedService(sp);
        Assert.NotNull(hostedService);

        var hostedServiceField = hostedService!.GetType().GetField("_registry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(hostedServiceField);

        var concreteRegistryType = hostedService.GetType().GetField("_registry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(concreteRegistryType);

        hostedServiceField!.SetValue(hostedService, registry);

        await hostedService.StartAsync(CancellationToken.None);

        Assert.True(okAdapter.IsStarted);
        Assert.False(throwingAdapter.IsStarted);
    }

    private sealed class ThrowingAdapter : IChannelAdapter
    {
        public string ChannelName { get; }
        public bool IsStarted { get; private set; }

        public ThrowingAdapter(string name) { ChannelName = name; }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
            => Task.CompletedTask;

        public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }

        public Task StartAsync(CancellationToken ct = default) => throw new InvalidOperationException("adapter failed to start");
        public Task StopAsync(CancellationToken ct = default) { IsStarted = false; return Task.CompletedTask; }
    }
}

public class OutboxMessageDefaultsTests
{
    [Fact]
    public void DefaultChannel_Constant_Should_Be_Default_String()
    {
        Assert.Equal("default", OutboxMessage.DefaultChannel);
    }

    [Fact]
    public void New_OutboxMessage_Should_Have_Sensible_Defaults()
    {
        var msg = new OutboxMessage();
        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.False(msg.IsPublished);
        Assert.Equal(0, msg.RetryCount);
        Assert.Equal(5, msg.MaxRetryCount);
        Assert.Equal(OutboxMessage.DefaultChannel, msg.Channel);
        Assert.Null(msg.PublishedAt);
        Assert.Null(msg.LastError);
        Assert.Null(msg.CorrelationId);
        Assert.Null(msg.NextRetryAt);
    }

    [Fact]
    public void Channel_Setter_Should_Accept_Custom_Value()
    {
        var msg = new OutboxMessage { Channel = "orders" };
        Assert.Equal("orders", msg.Channel);
    }
}

public class ProcessedEventTests
{
    [Fact]
    public void New_ProcessedEvent_Should_Default_ProcessedAt_To_Now()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var evt = new ProcessedEvent { EventId = Guid.NewGuid(), EventType = "Test" };
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.True(evt.ProcessedAt >= before);
        Assert.True(evt.ProcessedAt <= after);
    }
}

public class IdempotencyDecoratorIntegrationTests
{
    private sealed class InMemoryIdempotencyTracker : IIdempotencyTracker
    {
        public readonly HashSet<Guid> Processed = new();

        public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
            => Task.FromResult(Processed.Contains(eventId));

        public Task MarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
        {
            Processed.Add(eventId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler : IIntegrationEventHandler<DecoratorTestEvent>
    {
        public int Calls;
        public Task HandleAsync(DecoratorTestEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    public class DecoratorTestEvent : IntegrationEvent
    {
        public string Value { get; }
        public DecoratorTestEvent(string value) { Value = value; }
    }

    [Fact]
    public async Task First_Call_Should_Invoke_Handler_And_Mark_As_Processed()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var inner = new RecordingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
            inner, tracker, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>());

        var evt = new DecoratorTestEvent("a");
        await decorator.HandleAsync(evt);

        Assert.Equal(1, inner.Calls);
        Assert.Contains(evt.EventId, tracker.Processed);
    }

    [Fact]
    public async Task Second_Call_With_Same_EventId_Should_Be_Skipped()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var inner = new RecordingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
            inner, tracker, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>());

        var evt = new DecoratorTestEvent("a");
        await decorator.HandleAsync(evt);
        await decorator.HandleAsync(evt);

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Different_EventIds_Should_Both_Be_Processed()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var inner = new RecordingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
            inner, tracker, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>());

        await decorator.HandleAsync(new DecoratorTestEvent("a"));
        await decorator.HandleAsync(new DecoratorTestEvent("b"));

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Null_Event_Should_Throw()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var inner = new RecordingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
            inner, tracker, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => decorator.HandleAsync(null!));
    }

    [Fact]
    public void Constructor_With_Null_Inner_Should_Throw()
    {
        var tracker = new InMemoryIdempotencyTracker();
        Assert.Throws<ArgumentNullException>(() =>
            new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
                null!, tracker, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>()));
    }

    [Fact]
    public void Constructor_With_Null_Tracker_Should_Throw()
    {
        var inner = new RecordingHandler();
        Assert.Throws<ArgumentNullException>(() =>
            new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(
                inner, null!, new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>>()));
    }

    [Fact]
    public void Constructor_With_Null_Logger_Should_Throw()
    {
        var inner = new RecordingHandler();
        var tracker = new InMemoryIdempotencyTracker();
        Assert.Throws<ArgumentNullException>(() =>
            new IdempotentIntegrationEventHandlerDecorator<DecoratorTestEvent>(inner, tracker, null!));
    }
}

public class InMemoryChannelAdapterExtraTests
{
    [Fact]
    public async Task Start_Stop_Should_Toggle_IsStarted_Flag()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var adapter = new InMemoryChannelAdapter(
            "test",
            sp,
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());

        Assert.False(adapter.IsStarted);
        await adapter.StartAsync();
        Assert.True(adapter.IsStarted);
        await adapter.StopAsync();
        Assert.False(adapter.IsStarted);
    }

    [Fact]
    public async Task PublishAsync_With_No_Subscribers_Should_Succeed_Silently()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var adapter = new InMemoryChannelAdapter(
            "test",
            sp,
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());

        await adapter.PublishAsync(new PublisherTestEvent());
    }

    [Fact]
    public async Task Dispose_Should_Not_Throw()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var adapter = new InMemoryChannelAdapter(
            "test",
            sp,
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());

        adapter.Dispose();
        await adapter.DisposeAsync();
    }
}
