using Phantom.Data.EfCore;
using Phantom.Data.Specifications;
using Phantom.Data.Extensions;
using Phantom.Data.Outbox;
using Phantom.Data.Services;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Core.Specifications;
using Phantom.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;

namespace Phantom.Tests.Data;


public class TestProduct : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public TestProduct(Guid id, string name, decimal price) : base(id)
    {
        Name = name;
        Price = price;
    }

    public TestProduct() { }
}

public class TestSoftDeleteProduct : SoftDeleteEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public TestSoftDeleteProduct(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public TestSoftDeleteProduct() { }
}

public class TestAuditableOrder : AuditableEntity<Guid>
{
    public string OrderNumber { get; set; } = string.Empty;

    public TestAuditableOrder(Guid id, string orderNumber) : base(id)
    {
        OrderNumber = orderNumber;
    }

    public TestAuditableOrder() { }
}

public class TestFullEntity : AuditableSoftDeleteEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public TestFullEntity(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public TestFullEntity() { }
}


public class ExpensiveProductSpec : Specification<TestProduct>
{
    private readonly decimal _minPrice;

    public ExpensiveProductSpec(decimal minPrice) { _minPrice = minPrice; }

    public override bool IsSatisfiedBy(TestProduct candidate) => candidate.Price >= _minPrice;

    public override Expression<Func<TestProduct, bool>> ToExpression() => p => p.Price >= _minPrice;
}


public class TestDbContext : PhantomDbContext
{
    public TestDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }

    public DbSet<TestProduct> Products => Set<TestProduct>();
    public DbSet<TestSoftDeleteProduct> SoftDeleteProducts => Set<TestSoftDeleteProduct>();
    public DbSet<TestAuditableOrder> AuditableOrders => Set<TestAuditableOrder>();
    public DbSet<TestFullEntity> FullEntities => Set<TestFullEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestProduct>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<TestSoftDeleteProduct>(e =>
        {
            e.HasKey(p => p.Id);
        });

        modelBuilder.Entity<TestAuditableOrder>(e =>
        {
            e.HasKey(o => o.Id);
        });

        modelBuilder.Entity<TestFullEntity>(e =>
        {
            e.HasKey(f => f.Id);
        });

        modelBuilder.Entity<TestAggregateWithEvents>(e =>
        {
            e.HasKey(a => a.Id);
            e.Ignore(a => a.DomainEvents);
        });
    }
}


public class RepositoryTests
{
    private IServiceProvider BuildServiceProvider(bool useSoftDelete = false, bool useAuditable = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<TestDbContext>());

        services.AddSingleton<ISpecificationEvaluator, EfSpecificationEvaluator>();
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_Should_Work()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new TestProduct(id, "Widget", 9.99m));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var product = await repo.GetByIdAsync(id);

        Assert.NotNull(product);
        Assert.Equal("Widget", product!.Name);
        Assert.Equal(9.99m, product.Price);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Entities()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();

        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "A", 10));
        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "B", 20));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var all = await repo.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task FindAsync_With_Specification_Should_Filter()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();

        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "Cheap", 5));
        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "Expensive", 100));
        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "Premium", 200));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var spec = new ExpensiveProductSpec(50);
        var results = await repo.FindAsync(spec);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Price >= 50));
    }

    [Fact]
    public async Task CountAsync_Should_Return_Count()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();

        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "A", 10));
        await repo.AddAsync(new TestProduct(Guid.NewGuid(), "B", 20));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var count = await repo.CountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AnyAsync_Should_Check_Existence()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new TestProduct(id, "Widget", 10));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        Assert.True(await repo.AnyAsync(id));
        Assert.False(await repo.AnyAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPagedAsync_Should_Return_Paged_Results()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();

        for (int i = 0; i < 10; i++)
            await repo.AddAsync(new TestProduct(Guid.NewGuid(), $"Product-{i}", i));

        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var page = await repo.GetPagedAsync(skip: 5, take: 3);

        Assert.Equal(3, page.Count);
    }

    [Fact]
    public async Task UpdateAsync_Should_Modify_Entity()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new TestProduct(id, "Original", 10));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var product = (await repo.GetByIdAsync(id))!;
        product.Name = "Updated";
        await repo.UpdateAsync(product);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var updated = await repo.GetByIdAsync(id);
        Assert.Equal("Updated", updated!.Name);
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Entity()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IRepository<Guid, TestProduct>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new TestProduct(id, "ToDelete", 10));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var product = (await repo.GetByIdAsync(id))!;
        await repo.RemoveAsync(product);
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var deleted = await repo.GetByIdAsync(id);
        Assert.Null(deleted);
    }
}


public class UnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_Should_Return_Affected_Rows()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("UowTest_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var uow = sp.GetRequiredService<IUnitOfWork>();
        var dbContext = sp.GetRequiredService<TestDbContext>();
        dbContext.Products.Add(new TestProduct(Guid.NewGuid(), "Test", 10));

        var result = await uow.SaveChangesAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task BeginTransaction_Should_Work()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("TxTest_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var uow = sp.GetRequiredService<IUnitOfWork>();

        try
        {
            await uow.BeginTransactionAsync();
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void UnitOfWork_Should_Implement_IAsyncDisposable()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(UnitOfWork)));
    }
}


public class TestDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> DispatchedEvents { get; } = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        DispatchedEvents.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        DispatchedEvents.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}

public class TestAggregateWithEvents : AggregateRoot<Guid>
{
    public TestAggregateWithEvents(Guid id) : base(id) { }

    public void AddTestEvent(string data)
    {
        AddDomainEvent(new TestDomainEvent(data));
    }
}

public class TestDomainEvent : DomainEvent
{
    public string Data { get; }
    public TestDomainEvent(string data) { Data = data; }
}

public class PhantomDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_Should_Dispatch_Domain_Events()
    {
        var dispatcher = new TestDomainEventDispatcher();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("EventTest_" + Guid.NewGuid())
            .Options;

        using var context = new TestDbContext(options, dispatcher);
        var aggregate = new TestAggregateWithEvents(Guid.NewGuid());
        aggregate.AddTestEvent("event1");
        aggregate.AddTestEvent("event2");

        context.Set<TestAggregateWithEvents>().Add(aggregate);
        await context.SaveChangesAsync();

        Assert.Equal(2, dispatcher.DispatchedEvents.Count);
    }

    [Fact]
    public async Task DomainEvents_Should_Be_Cleared_After_Dispatch()
    {
        var dispatcher = new TestDomainEventDispatcher();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ClearEventTest_" + Guid.NewGuid())
            .Options;

        using var context = new TestDbContext(options, dispatcher);
        var aggregate = new TestAggregateWithEvents(Guid.NewGuid());
        aggregate.AddTestEvent("event1");

        context.Set<TestAggregateWithEvents>().Add(aggregate);
        await context.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task Failed_Handler_Should_Not_Lose_Other_Events()
    {
        var dispatcher = new ThrowingDomainEventDispatcher(failOnNth: 1);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("FailedEventTest_" + Guid.NewGuid())
            .Options;

        using var context = new TestDbContext(options, dispatcher);
        var aggregate = new TestAggregateWithEvents(Guid.NewGuid());
        aggregate.AddTestEvent("event1");
        aggregate.AddTestEvent("event2");

        context.Set<TestAggregateWithEvents>().Add(aggregate);
        await context.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
    }
}

public class ThrowingDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly int _failOnNth;
    private int _count;

    public ThrowingDomainEventDispatcher(int failOnNth) { _failOnNth = failOnNth; }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _count++;
        if (_count == _failOnNth)
            throw new InvalidOperationException($"Handler failed for event {_count}");
        return Task.CompletedTask;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var e in domainEvents)
            DispatchAsync(e, cancellationToken);
        return Task.CompletedTask;
    }
}


public class EfOutboxRepositoryTests
{
    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("OutboxTest_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddScoped<IOutboxMessageRepository, EfOutboxRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddAsync_And_GetPendingAsync_Should_Work()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<TestDbContext>();

        var msg = new OutboxMessage
        {
            EventType = typeof(TestDataIntegrationEvent).AssemblyQualifiedName!,
            Payload = "{\"OrderId\":\"ORD-1\"}",
            Channel = "default"
        };

        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);

        Assert.Single(pending);
        Assert.Equal(msg.Id, pending[0].Id);
    }

    [Fact]
    public async Task MarkAsPublishedAsync_Should_Mark_As_Published()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<TestDbContext>();

        var msg = new OutboxMessage
        {
            EventType = typeof(TestDataIntegrationEvent).AssemblyQualifiedName!,
            Payload = "{}",
            Channel = "default"
        };

        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.MarkAsPublishedAsync(msg.Id);
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Record_Error()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<TestDbContext>();

        var msg = new OutboxMessage
        {
            EventType = typeof(TestDataIntegrationEvent).AssemblyQualifiedName!,
            Payload = "{}",
            Channel = "default"
        };

        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.MarkAsFailedAsync(msg.Id, "Connection timeout");
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal("Connection timeout", pending[0].LastError);
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Increment()
    {
        var sp = BuildServiceProvider();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<TestDbContext>();

        var msg = new OutboxMessage
        {
            EventType = typeof(TestDataIntegrationEvent).AssemblyQualifiedName!,
            Payload = "{}",
            Channel = "default"
        };

        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.IncrementRetryCountAsync(msg.Id, "retry error");
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(10);
        Assert.Single(pending);
        Assert.Equal(1, pending[0].RetryCount);
        Assert.Equal("retry error", pending[0].LastError);
    }
}


public class SpecificationEvaluatorTests
{
    [Fact]
    public void ApplySpecification_Should_Translate_To_SQL_Compatible_Expression()
    {
        var evaluator = new EfSpecificationEvaluator();
        var spec = new ExpensiveProductSpec(50m);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("SpecTest_" + Guid.NewGuid())
            .Options;

        using var context = new TestDbContext(options);
        var query = evaluator.ApplySpecification(context.Products, spec);

        var results = query.ToList();
        Assert.NotNull(results);
    }
}

public class TestDataIntegrationEvent : IntegrationEvent
{
    public string OrderId { get; }
    public TestDataIntegrationEvent(string orderId) { OrderId = orderId; }
}
