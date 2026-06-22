using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.Data.EfCore;
using Phantom.Data.Idempotency;
using Phantom.Data.Outbox;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.Messaging.Outbox;
using Phantom.NET.Middleware;
using System.Net;

namespace Phantom.Tests.FaultTolerance;

public class TestFaultToleranceEvent : IntegrationEvent
{
    public string Payload { get; }
    public TestFaultToleranceEvent(string payload) { Payload = payload; }
}

public class TestFaultToleranceDomainEvent : DomainEvent
{
    public string Data { get; }
    public TestFaultToleranceDomainEvent(string data) { Data = data; }
}

public class FaultToleranceTestAggregate : Phantom.Core.Domain.AggregateRoot<Guid>
{
    public FaultToleranceTestAggregate(Guid id) : base(id) { }
    public void RaiseEvent(string data) => AddDomainEvent(new TestFaultToleranceDomainEvent(data));
}

public class FaultToleranceDbContext : PhantomDbContext
{
    public FaultToleranceDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null, IMessageSerializer? serializer = null)
        : base(options, dispatcher, null, serializer) { }

    public DbSet<FaultToleranceTestAggregate> Aggregates => Set<FaultToleranceTestAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FaultToleranceTestAggregate>(e =>
        {
            e.HasKey(a => a.Id);
            e.Ignore(a => a.DomainEvents);
        });
    }
}

public class OutboxProcessorFaultToleranceTests
{
    private static OutboxMessage MakeMessage(string? eventType = null, string? payload = null, int retryCount = 0, int maxRetryCount = 5)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType ?? typeof(TestFaultToleranceEvent).AssemblyQualifiedName!,
            Payload = payload ?? "{}",
            Channel = OutboxMessage.DefaultChannel,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount
        };
    }

    private static (Mock<IOutboxMessageRepository> repo, Mock<IEventPublisher> publisher, IServiceProvider sp) BuildHarness()
    {
        var repo = new Mock<IOutboxMessageRepository>();
        var publisher = new Mock<IEventPublisher>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(repo.Object);
        services.AddSingleton(publisher.Object);
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        return (repo, publisher, services.BuildServiceProvider());
    }

    private static OutboxProcessor BuildProcessor(IServiceProvider sp, IMessageSerializer serializer)
    {
        return new OutboxProcessor(
            sp,
            serializer,
            NullResiliencePipeline.Instance,
            new LoggerFactory().CreateLogger<OutboxProcessor>(),
            batchSize: 10,
            pollingInterval: TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ProcessAsync_Should_Skip_Message_When_Lock_Not_Acquired()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never,
            "When the processing lock is not acquired, the message must not be published.");
        repo.Verify(r => r.MarkAsPublishedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ClearProcessingLockAsync(message.Id, It.IsAny<CancellationToken>()), Times.Never,
            "When the lock was never acquired, ClearProcessingLockAsync must not be called.");
    }

    [Fact]
    public async Task ProcessAsync_Should_Clear_Lock_In_Finally_Block_After_Successful_Publish()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        repo.Verify(r => r.ClearProcessingLockAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once,
            "Lock must be released after successful publish so the next iteration can re-evaluate.");
    }

    [Fact]
    public async Task ProcessAsync_Should_Clear_Lock_In_Finally_Block_After_Publish_Failure()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        publisher.Setup(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("broker down"));

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        repo.Verify(r => r.ClearProcessingLockAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once,
            "Lock must be released after publish failure so a future iteration can retry.");
        repo.Verify(r => r.IncrementRetryCountAsync(message.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.MarkAsPublishedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_Should_Mark_Terminal_Failure_When_Type_Cannot_Be_Resolved()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage(eventType: "Phantom.NonExistent.Type, Phantom.NonExistent");
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        repo.Verify(r => r.MarkAsTerminalFailureAsync(message.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "Type resolution failures must be marked as terminal so they stop being retried forever.");
        repo.Verify(r => r.IncrementRetryCountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "Terminal failures must not be retried.");
        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_Should_Mark_Terminal_Failure_When_Deserialization_Fails()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage(payload: "this is not valid JSON");
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        repo.Verify(r => r.MarkAsTerminalFailureAsync(message.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "Deserialization failures must be marked as terminal because the payload will never deserialize.");
        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_Should_Still_Clear_Lock_When_MarkAsPublishedAsync_Throws()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.MarkAsPublishedAsync(message.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once,
            "Publish must have happened before MarkAsPublishedAsync was attempted.");
        repo.Verify(r => r.ClearProcessingLockAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once,
            "Lock must still be released even if MarkAsPublishedAsync threw.");
    }

    [Fact]
    public async Task ProcessAsync_Should_Clear_Lock_When_Lock_Acquisition_Throws()
    {
        var (repo, publisher, sp) = BuildHarness();
        var message = MakeMessage();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessage> { message });
        repo.Setup(r => r.TryMarkAsProcessingAsync(message.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var serializer = sp.GetRequiredService<IMessageSerializer>();
        var processor = BuildProcessor(sp, serializer);

        await processor.ProcessAsync(CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ClearProcessingLockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "When the lock could not be acquired due to an exception, Clear must not be called.");
    }
}

public class EfOutboxRepositoryFaultToleranceTests
{
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FaultToleranceDbContext>(o => o.UseInMemoryDatabase("OutboxFT_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<FaultToleranceDbContext>());
        services.AddScoped<IOutboxMessageRepository, EfOutboxRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services.BuildServiceProvider();
    }

    private static OutboxMessage MakeMessage(int retryCount = 0, int maxRetryCount = 5)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TestFaultToleranceEvent).AssemblyQualifiedName!,
            Payload = "{\"Payload\":\"x\"}",
            Channel = OutboxMessage.DefaultChannel,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount
        };
    }

    [Fact]
    public async Task GetPendingAsync_Should_Exclude_Messages_At_MaxRetryCount()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var exhausted = MakeMessage(retryCount: 5, maxRetryCount: 5);
        var fresh = MakeMessage(retryCount: 0, maxRetryCount: 5);

        await repo.AddAsync(exhausted);
        await repo.AddAsync(fresh);
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.DoesNotContain(pending, m => m.Id == exhausted.Id);
        Assert.Contains(pending, m => m.Id == fresh.Id);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Exclude_Messages_With_Future_NextRetryAt()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var backoff = MakeMessage();
        backoff.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var ready = MakeMessage();
        ready.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        await repo.AddAsync(backoff);
        await repo.AddAsync(ready);
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.DoesNotContain(pending, m => m.Id == backoff.Id);
        Assert.Contains(pending, m => m.Id == ready.Id);
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Set_Exponential_Backoff()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage();
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        await repo.IncrementRetryCountAsync(msg.Id, "first failure");
        await dbContext.SaveChangesAsync();

        var stored = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.NotNull(stored);
        Assert.Equal(1, stored!.RetryCount);
        Assert.NotNull(stored.NextRetryAt);
        Assert.True(stored.NextRetryAt >= before.AddSeconds(5),
            $"First retry backoff must be at least 5s in the future, got {stored.NextRetryAt} (before={before}).");
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Clear_NextRetryAt_At_MaxRetryCount()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage(retryCount: 4, maxRetryCount: 5);
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.IncrementRetryCountAsync(msg.Id, "final failure");
        await dbContext.SaveChangesAsync();

        var stored = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.NotNull(stored);
        Assert.Equal(5, stored!.RetryCount);
        Assert.Null(stored.NextRetryAt);
        Assert.True(stored.IsTerminalFailure);
    }

    [Fact]
    public async Task TryMarkAsProcessingAsync_Should_Return_False_When_Already_Locked()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage();
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        var firstAcquired = await repo.TryMarkAsProcessingAsync(msg.Id, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var secondAcquired = await repo.TryMarkAsProcessingAsync(msg.Id, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.True(firstAcquired);
        Assert.False(secondAcquired, "A second concurrent worker must not acquire the lock on the same message.");
    }

    [Fact]
    public async Task ClearProcessingLockAsync_Should_Release_Lock_For_Retry()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage();
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        var firstAcquired = await repo.TryMarkAsProcessingAsync(msg.Id, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await repo.ClearProcessingLockAsync(msg.Id, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var reacquired = await repo.TryMarkAsProcessingAsync(msg.Id, DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.True(firstAcquired);
        Assert.True(reacquired, "After ClearProcessingLockAsync, the message must be available for re-acquisition.");
    }

    [Fact]
    public async Task MarkAsTerminalFailureAsync_Should_Stop_Retries()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage(retryCount: 0, maxRetryCount: 5);
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.MarkAsTerminalFailureAsync(msg.Id, "fatal error", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.DoesNotContain(pending, m => m.Id == msg.Id);

        var stored = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.True(stored!.IsTerminalFailure);
        Assert.Equal(5, stored.RetryCount);
    }

    [Fact]
    public async Task MarkAsPublishedAsync_Should_Clear_Retry_Metadata()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();

        var msg = MakeMessage(retryCount: 2);
        msg.LastError = "previous failure";
        msg.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.MarkAsPublishedAsync(msg.Id, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var stored = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.True(stored!.IsPublished);
        Assert.Null(stored.LastError);
        Assert.Null(stored.NextRetryAt);
        Assert.NotNull(stored.PublishedAt);
    }
}

public class EfIdempotencyTrackerFaultToleranceTests
{
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FaultToleranceDbContext>(o => o.UseInMemoryDatabase("IdempotencyFT_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<FaultToleranceDbContext>());
        services.AddScoped<IIdempotencyTracker, EfIdempotencyTracker>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_Should_Return_True_On_First_Call_And_False_On_Second()
    {
        var sp = BuildServiceProvider();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();
        var eventId = Guid.NewGuid();

        var first = await tracker.TryMarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        var second = await tracker.TryMarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        Assert.True(first);
        Assert.False(second, "Second call must return false because the event is already marked as processed.");
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Be_Idempotent_On_Duplicate()
    {
        var sp = BuildServiceProvider();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();
        var eventId = Guid.NewGuid();

        await tracker.MarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        await tracker.MarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        var isProcessed = await tracker.IsProcessedAsync(eventId);
        Assert.True(isProcessed);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Be_Idempotent_When_Called_Multiple_Times_Sequentially()
    {
        var sp = BuildServiceProvider();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();
        var eventId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await tracker.MarkAsProcessedAsync(eventId, "TestEvent");
            await dbContext.SaveChangesAsync();
        }

        var count = await dbContext.Set<ProcessedEvent>().CountAsync(e => e.EventId == eventId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TryMarkAsProcessedAsync_Should_Return_True_Only_For_First_Call_After_SaveChanges()
    {
        var sp = BuildServiceProvider();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<FaultToleranceDbContext>();
        var eventId = Guid.NewGuid();

        var first = await tracker.TryMarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        var second = await tracker.TryMarkAsProcessedAsync(eventId, "TestEvent");
        await dbContext.SaveChangesAsync();

        Assert.True(first);
        Assert.False(second);
    }
}

public class UnitOfWorkFaultToleranceTests
{
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FaultToleranceDbContext>(o =>
            o.UseInMemoryDatabase("UoWFT_" + Guid.NewGuid())
             .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<FaultToleranceDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task BeginTransactionAsync_Should_Throw_If_Transaction_Already_Active()
    {
        var sp = BuildServiceProvider();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        await uow.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.BeginTransactionAsync());
    }

    [Fact]
    public async Task CommitAsync_Should_Dispose_Transaction_Even_On_Failure()
    {
        var sp = BuildServiceProvider();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        await uow.BeginTransactionAsync();
        await uow.RollbackAsync();

        await uow.BeginTransactionAsync();
        await uow.CommitAsync();
    }

    [Fact]
    public async Task Dispose_Should_Rollback_Uncommitted_Transaction()
    {
        var sp = BuildServiceProvider();
        var scope1 = sp.CreateScope();
        var uow1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await uow1.BeginTransactionAsync();
        uow1.Dispose();

        var scope2 = sp.CreateScope();
        var uow2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow2.BeginTransactionAsync();
        await uow2.CommitAsync();
    }

    [Fact]
    public async Task Operations_After_Dispose_Should_Throw_ObjectDisposedException()
    {
        var sp = BuildServiceProvider();
        var uow = sp.GetRequiredService<IUnitOfWork>();
        uow.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => uow.BeginTransactionAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => uow.CommitAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => uow.RollbackAsync());
    }

    [Fact]
    public async Task RollbackAsync_Without_Transaction_Should_Be_NoOp()
    {
        var sp = BuildServiceProvider();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        await uow.RollbackAsync();
        await uow.BeginTransactionAsync();
        await uow.CommitAsync();
    }

    [Fact]
    public async Task CommitAsync_Without_Transaction_Should_Be_NoOp()
    {
        var sp = BuildServiceProvider();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        await uow.CommitAsync();
        await uow.BeginTransactionAsync();
        await uow.CommitAsync();
    }
}

public class ChannelRegistryFaultToleranceTests
{
    private static ILogger<ChannelRegistry> BuildLogger() =>
        new LoggerFactory().CreateLogger<ChannelRegistry>();

    private class FakeAdapter : IChannelAdapter
    {
        public string ChannelName { get; } = "fake";
        public bool IsStarted { get; set; }
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
            => Task.CompletedTask;
        public void Subscribe<TEvent, THandler>() where TEvent : IIntegrationEvent where THandler : IIntegrationEventHandler<TEvent> { }
        public Task StartAsync(CancellationToken ct = default) { IsStarted = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) { IsStarted = false; return Task.CompletedTask; }
    }

    private class TestRoutingEvent : IntegrationEvent { }

    [Fact]
    public void Register_Should_Skip_Duplicate_Adapter_Instance()
    {
        var registry = new ChannelRegistry(BuildLogger());
        var adapter = new FakeAdapter();

        registry.Register("orders", adapter);
        registry.Register("orders", adapter);

        var channels = registry.GetChannels("orders");
        Assert.Single(channels);
    }

    [Fact]
    public void GetChannelsForEvent_Should_Deduplicate_Adapters_Across_Channels()
    {
        var registry = new ChannelRegistry(BuildLogger());
        var sharedAdapter = new FakeAdapter();

        registry.Register("orders", sharedAdapter);
        registry.Register("notifications", sharedAdapter);
        registry.MapEventToChannel<TestRoutingEvent>("orders");
        registry.MapEventToChannel<TestRoutingEvent>("notifications");

        var adapters = registry.GetChannelsForEvent<TestRoutingEvent>();
        Assert.Single(adapters);
    }

    [Fact]
    public async Task GetChannelsForEvent_Should_Return_Stable_Snapshot_Under_Concurrent_Register()
    {
        var registry = new ChannelRegistry(BuildLogger());

        registry.Register("orders", new FakeAdapter());
        registry.MapEventToChannel<TestRoutingEvent>("orders");

        var readTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var adapters = registry.GetChannelsForEvent<TestRoutingEvent>();
                Assert.NotNull(adapters);
            }
        });

        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                registry.Register($"channel-{i}", new FakeAdapter());
            }
        });

        await Task.WhenAll(readTask, writeTask);
    }

    [Fact]
    public void GetAllAdapters_Should_Deduplicate_Across_Channels()
    {
        var registry = new ChannelRegistry(BuildLogger());
        var shared = new FakeAdapter();

        registry.Register("a", shared);
        registry.Register("b", shared);
        registry.Register("c", new FakeAdapter());

        var all = registry.GetAllAdapters();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Register_With_Empty_Name_Should_Throw()
    {
        var registry = new ChannelRegistry(BuildLogger());
        Assert.Throws<ArgumentException>(() => registry.Register("", new FakeAdapter()));
        Assert.Throws<ArgumentException>(() => registry.Register("   ", new FakeAdapter()));
    }

    [Fact]
    public void Register_With_Null_Adapter_Should_Throw()
    {
        var registry = new ChannelRegistry(BuildLogger());
        Assert.Throws<ArgumentNullException>(() => registry.Register("orders", null!));
    }
}

public class InMemoryChannelAdapterFaultToleranceTests
{
    private class FailingHandler : IIntegrationEventHandler<TestFaultToleranceEvent>
    {
        public int Calls;
        public Task HandleAsync(TestFaultToleranceEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("handler failure");
        }
    }

    private class CountingHandler : IIntegrationEventHandler<TestFaultToleranceEvent>
    {
        public int Calls;
        public Task HandleAsync(TestFaultToleranceEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    private static IServiceProvider BuildServiceProviderWithHandlers(params IIntegrationEventHandler<TestFaultToleranceEvent>[] handlers)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        foreach (var h in handlers)
            services.AddSingleton(h.GetType(), h);
        return services.BuildServiceProvider();
    }

    private static InMemoryChannelAdapter BuildAdapter(IServiceProvider sp)
    {
        return new InMemoryChannelAdapter(
            "test",
            sp,
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());
    }

    private static IServiceProvider BuildServiceProviderWithFailing(FailingHandler failing)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIntegrationEventHandler<TestFaultToleranceEvent>>(failing);
        return services.BuildServiceProvider();
    }

    private static InMemoryChannelAdapter BuildAdapterWithDirectInvokers(
        IServiceProvider sp,
        params Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>[] invokers)
    {
        var adapter = new InMemoryChannelAdapter(
            "test",
            sp,
            new LoggerFactory().CreateLogger<InMemoryChannelAdapter>());

        var handlersField = typeof(InMemoryChannelAdapter)
            .GetField("_handlers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (handlersField is null)
            throw new InvalidOperationException("Could not find _handlers field on InMemoryChannelAdapter via reflection.");

        var handlers = handlersField.GetValue(adapter) as System.Collections.Concurrent.ConcurrentDictionary<Type, List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>>;
        if (handlers is null)
            throw new InvalidOperationException("_handlers field value is null or wrong type.");

        var list = handlers.GetOrAdd(typeof(TestFaultToleranceEvent), _ => new List<Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>>());
        lock (list)
        {
            foreach (var inv in invokers)
                list.Add(inv);
        }
        return adapter;
    }

    [Fact]
    public async Task PublishAsync_Should_Not_Throw_When_Handler_Throws()
    {
        var failing = new FailingHandler();
        var sp = BuildServiceProviderWithFailing(failing);
        var adapter = BuildAdapter(sp);

        adapter.Subscribe<TestFaultToleranceEvent, FailingHandler>();

        await adapter.PublishAsync(new TestFaultToleranceEvent("payload"));

        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public async Task PublishAsync_Should_Call_All_Handlers_Even_When_One_Fails()
    {
        var counting = new CountingHandler();
        var failing = new FailingHandler();
        var sp = BuildServiceProviderWithHandlers(counting, failing);

        var adapter = BuildAdapterWithDirectInvokers(
            sp,
            (s, e, ct) => s.GetRequiredService<CountingHandler>().HandleAsync((TestFaultToleranceEvent)e, ct),
            (s, e, ct) => s.GetRequiredService<FailingHandler>().HandleAsync((TestFaultToleranceEvent)e, ct));

        await adapter.PublishAsync(new TestFaultToleranceEvent("payload"));

        Assert.Equal(1, counting.Calls);
        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public async Task PublishAsync_After_Dispose_Should_Throw_ObjectDisposedException()
    {
        var failing = new FailingHandler();
        var sp = BuildServiceProviderWithFailing(failing);
        var adapter = BuildAdapter(sp);

        adapter.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            adapter.PublishAsync(new TestFaultToleranceEvent("payload")));
    }

    [Fact]
    public async Task StartAsync_After_Dispose_Should_Throw_ObjectDisposedException()
    {
        var failing = new FailingHandler();
        var sp = BuildServiceProviderWithFailing(failing);
        var adapter = BuildAdapter(sp);

        adapter.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => adapter.StartAsync());
    }

    [Fact]
    public async Task PublishAsync_With_No_Handlers_Should_Be_NoOp()
    {
        var failing = new FailingHandler();
        var sp = BuildServiceProviderWithFailing(failing);
        var adapter = BuildAdapter(sp);

        await adapter.PublishAsync(new TestFaultToleranceEvent("payload"));
    }

    [Fact]
    public async Task PublishAsync_Should_Be_Safe_Under_Concurrent_Publish()
    {
        var counting = new CountingHandler();
        var sp = BuildServiceProviderWithHandlers(counting);

        var adapter = BuildAdapterWithDirectInvokers(
            sp,
            (s, e, ct) => s.GetRequiredService<CountingHandler>().HandleAsync((TestFaultToleranceEvent)e, ct));

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => adapter.PublishAsync(new TestFaultToleranceEvent("payload")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(50, counting.Calls);
    }
}

public class DomainEventDispatcherFaultToleranceTests
{
    private class ThrowingDomainEventHandler : IDomainEventHandler<TestFaultToleranceDomainEvent>
    {
        public Task HandleAsync(TestFaultToleranceDomainEvent domainEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }

    private class CountingDomainEventHandler : IDomainEventHandler<TestFaultToleranceDomainEvent>
    {
        public int Calls;
        public Task HandleAsync(TestFaultToleranceDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    private static IServiceProvider BuildServiceProviderWithHandler<THandler>(THandler handler) where THandler : class, IDomainEventHandler<TestFaultToleranceDomainEvent>
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDomainEventHandler<TestFaultToleranceDomainEvent>>(handler);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider BuildServiceProviderWithHandler<THandler>(THandler handler, Type handlerType) where THandler : class, IDomainEventHandler<TestFaultToleranceDomainEvent>
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(typeof(IDomainEventHandler<TestFaultToleranceDomainEvent>), handlerType);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider BuildEmptyServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DispatchAsync_Should_Throw_AggregateException_When_Handlers_Fail()
    {
        var handler = new ThrowingDomainEventHandler();
        var sp = BuildServiceProviderWithHandler(handler);
        var dispatcher = new DomainEventDispatcher(sp, new LoggerFactory().CreateLogger<DomainEventDispatcher>());

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            dispatcher.DispatchAsync(new TestFaultToleranceDomainEvent("data")));

        Assert.Single(ex.InnerExceptions);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
    }

    [Fact]
    public async Task DispatchAsync_Should_Unwrap_TargetInvocationException()
    {
        var handler = new ThrowingDomainEventHandler();
        var sp = BuildServiceProviderWithHandler(handler);
        var dispatcher = new DomainEventDispatcher(sp, new LoggerFactory().CreateLogger<DomainEventDispatcher>());

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            dispatcher.DispatchAsync(new TestFaultToleranceDomainEvent("data")));

        Assert.IsNotType<System.Reflection.TargetInvocationException>(ex.InnerExceptions[0]);
    }

    [Fact]
    public async Task DispatchAsync_Should_Continue_After_Handler_Failure()
    {
        var counting = new CountingDomainEventHandler();
        var throwing = new ThrowingDomainEventHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDomainEventHandler<TestFaultToleranceDomainEvent>>(throwing);
        services.AddSingleton<IDomainEventHandler<TestFaultToleranceDomainEvent>>(counting);
        var sp = services.BuildServiceProvider();

        var dispatcher = new DomainEventDispatcher(sp, new LoggerFactory().CreateLogger<DomainEventDispatcher>());

        await Assert.ThrowsAsync<AggregateException>(() =>
            dispatcher.DispatchAsync(new TestFaultToleranceDomainEvent("data")));

        Assert.Equal(1, counting.Calls);
    }

    [Fact]
    public async Task DispatchAsync_Should_Throw_ArgumentNullException_For_Null_Event()
    {
        var sp = BuildEmptyServiceProvider();
        var dispatcher = new DomainEventDispatcher(sp, new LoggerFactory().CreateLogger<DomainEventDispatcher>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync((TestFaultToleranceDomainEvent)null!));
    }

    [Fact]
    public async Task DispatchAsync_Batch_Should_Stop_On_Cancellation()
    {
        var counting = new CountingDomainEventHandler();
        var sp = BuildServiceProviderWithHandler(counting);
        var dispatcher = new DomainEventDispatcher(sp, new LoggerFactory().CreateLogger<DomainEventDispatcher>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await dispatcher.DispatchAsync(new[] { new TestFaultToleranceDomainEvent("a") }, cts.Token);
        Assert.Equal(0, counting.Calls);
    }
}

public class PhantomDbContextFaultToleranceTests
{
    private class FailingDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("dispatch failure");
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("dispatch failure");
    }

    private static FaultToleranceDbContext BuildContext(IDomainEventDispatcher? dispatcher = null, IMessageSerializer? serializer = null)
    {
        var options = new DbContextOptionsBuilder<FaultToleranceDbContext>()
            .UseInMemoryDatabase("DbContextFT_" + Guid.NewGuid())
            .Options;
        return new FaultToleranceDbContext(options, dispatcher, serializer);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Not_Throw_When_Domain_Event_Dispatch_Fails()
    {
        var dispatcher = new FailingDomainEventDispatcher();
        using var context = BuildContext(dispatcher);

        var aggregate = new FaultToleranceTestAggregate(Guid.NewGuid());
        aggregate.RaiseEvent("event");
        context.Aggregates.Add(aggregate);

        await context.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Persist_Aggregate_Even_When_Dispatcher_Throws()
    {
        var dispatcher = new FailingDomainEventDispatcher();
        using var context = BuildContext(dispatcher);

        var aggregate = new FaultToleranceTestAggregate(Guid.NewGuid());
        aggregate.RaiseEvent("event");
        context.Aggregates.Add(aggregate);

        await context.SaveChangesAsync();

        var stored = await context.Aggregates.FindAsync(aggregate.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Enqueue_Outbox_When_Serializer_Provided()
    {
        using var context = BuildContext(serializer: new JsonMessageSerializer());

        var aggregate = new FaultToleranceTestAggregate(Guid.NewGuid());
        aggregate.RaiseEvent("event");
        context.Aggregates.Add(aggregate);

        await context.SaveChangesAsync();

        var outboxCount = await context.Set<OutboxMessage>().CountAsync();
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Continue_Even_When_One_Event_Serialization_Fails()
    {
        var failingSerializer = new ThrowingMessageSerializer();
        using var context = BuildContext(serializer: failingSerializer);

        var aggregate = new FaultToleranceTestAggregate(Guid.NewGuid());
        aggregate.RaiseEvent("event1");
        aggregate.RaiseEvent("event2");
        context.Aggregates.Add(aggregate);

        await context.SaveChangesAsync();

        var outboxCount = await context.Set<OutboxMessage>().CountAsync();
        Assert.Equal(0, outboxCount);
        Assert.Equal(2, failingSerializer.Attempts);
    }

    private class ThrowingMessageSerializer : IMessageSerializer
    {
        public int Attempts;
        public string ContentType => "application/json";
        public byte[] Serialize<T>(T message)
        {
            Interlocked.Increment(ref Attempts);
            throw new InvalidOperationException("serialize failure");
        }
        public T Deserialize<T>(byte[] data) => default!;
        public object Deserialize(byte[] data, Type type) => null!;
        public (bool Success, T? Value) TryDeserialize<T>(byte[] data) => (false, default);
    }
}

public class ExceptionHandlingMiddlewareFaultToleranceTests
{
    private static async Task InvokeMiddleware(Exception exceptionThrownByNext, HttpContext context)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exceptionThrownByNext,
            new LoggerFactory().CreateLogger<ExceptionHandlingMiddleware>());

        await middleware.InvokeAsync(context);
    }

    private static DefaultHttpContext BuildContext(out MemoryStream responseBody)
    {
        responseBody = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = responseBody;
        context.Request.Path = "/api/test";
        context.TraceIdentifier = Guid.NewGuid().ToString();
        return context;
    }

    private static string ReadBody(MemoryStream stream)
    {
        stream.Position = 0;
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task Should_Return_500_For_Generic_Exception()
    {
        var context = BuildContext(out var body);
        await InvokeMiddleware(new InvalidOperationException("boom"), context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Contains("Internal Server Error", ReadBody(body));
    }

    [Fact]
    public async Task Should_Return_504_For_TimeoutException()
    {
        var context = BuildContext(out var body);
        await InvokeMiddleware(new TimeoutException("operation timed out"), context);

        Assert.Equal((int)HttpStatusCode.GatewayTimeout, context.Response.StatusCode);
        Assert.Contains("operation timed out", ReadBody(body));
    }

    [Fact]
    public async Task Should_Return_499_For_Client_Cancellation()
    {
        var context = BuildContext(out var body);
        context.RequestAborted = new CancellationToken(canceled: true);

        await InvokeMiddleware(new OperationCanceledException(), context);

        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Should_Set_Problem_Details_Content_Type()
    {
        var context = BuildContext(out _);
        await InvokeMiddleware(new InvalidOperationException("boom"), context);

        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task Should_Include_TraceId_In_Problem_Details()
    {
        var context = BuildContext(out var body);
        await InvokeMiddleware(new InvalidOperationException("boom"), context);

        Assert.Contains(context.TraceIdentifier, ReadBody(body));
    }

    private class CannotWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("response already sent");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new InvalidOperationException("response already sent");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new InvalidOperationException("response already sent");
    }
}

public class IdempotentDecoratorFaultToleranceTests
{
    private sealed class InMemoryIdempotencyTracker : IIdempotencyTracker
    {
        public readonly HashSet<Guid> Processed = new();
        private readonly object _lock = new();
        private Exception? _markAsProcessedException;

        public void FailNextMarkAsProcessed(Exception ex) => _markAsProcessedException = ex;

        public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        {
            lock (_lock) { return Task.FromResult(Processed.Contains(eventId)); }
        }

        public Task MarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_markAsProcessedException is not null)
                {
                    var ex = _markAsProcessedException;
                    _markAsProcessedException = null;
                    throw ex;
                }
                Processed.Add(eventId);
            }
            return Task.CompletedTask;
        }

        public Task<bool> TryMarkAsProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (Processed.Contains(eventId)) return Task.FromResult(false);
                if (_markAsProcessedException is not null)
                {
                    var ex = _markAsProcessedException;
                    _markAsProcessedException = null;
                    throw ex;
                }
                Processed.Add(eventId);
                return Task.FromResult(true);
            }
        }
    }

    private class CountingHandler : IIntegrationEventHandler<TestFaultToleranceEvent>
    {
        public int Calls;
        public Task HandleAsync(TestFaultToleranceEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    private class ThrowingHandler : IIntegrationEventHandler<TestFaultToleranceEvent>
    {
        public Task HandleAsync(TestFaultToleranceEvent integrationEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }

    private static ILogger<IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>> BuildLogger() =>
        new LoggerFactory().CreateLogger<IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>>();

    [Fact]
    public async Task HandleAsync_Should_Skip_Event_Already_Marked_As_Processed()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var handler = new CountingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>(
            handler, tracker, BuildLogger());

        var evt = new TestFaultToleranceEvent("x");
        await tracker.MarkAsProcessedAsync(evt.EventId, "TestEvent");

        await decorator.HandleAsync(evt);

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task HandleAsync_Should_Invoke_Handler_For_Unprocessed_Event()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var handler = new CountingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>(
            handler, tracker, BuildLogger());

        var evt = new TestFaultToleranceEvent("x");
        await decorator.HandleAsync(evt);

        Assert.Equal(1, handler.Calls);
        Assert.Contains(evt.EventId, tracker.Processed);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Throw_When_MarkAsProcessed_Fails()
    {
        var tracker = new InMemoryIdempotencyTracker();
        tracker.FailNextMarkAsProcessed(new InvalidOperationException("DB down"));
        var handler = new CountingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>(
            handler, tracker, BuildLogger());

        var evt = new TestFaultToleranceEvent("x");
        await decorator.HandleAsync(evt);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task HandleAsync_Should_Propagate_Handler_Exception()
    {
        var tracker = new InMemoryIdempotencyTracker();
        var throwingHandler = new ThrowingHandler();
        var decorator = new IdempotentIntegrationEventHandlerDecorator<TestFaultToleranceEvent>(
            throwingHandler, tracker, BuildLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            decorator.HandleAsync(new TestFaultToleranceEvent("x")));

        Assert.False(tracker.Processed.Any(),
            "When the handler throws, the event must not be marked as processed so it can be retried on redelivery.");
    }
}
