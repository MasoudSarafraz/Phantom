using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Core.Specifications;
using Phantom.Data.EfCore;
using Phantom.Data.Extensions;
using Phantom.Data.Idempotency;
using Phantom.Data.Interceptors;
using Phantom.Data.Outbox;
using Phantom.Data.Specifications;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Abstractions;
using System.Linq.Expressions;

namespace Phantom.Tests.Data;

public class InterceptorTestEntity : AuditableSoftDeleteEntity<Guid>, IAuditable
{
    public string Name { get; set; } = string.Empty;
    public InterceptorTestEntity() { }
    public InterceptorTestEntity(Guid id, string name) : base(id) { Name = name; }
}

public class InterceptorTestDbContext : PhantomDbContext
{
    public InterceptorTestDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }

    public DbSet<InterceptorTestEntity> Items => Set<InterceptorTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InterceptorTestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });
    }
}

public class CurrentUserServiceStub : ICurrentUserService
{
    public string? User { get; set; }
    public string? GetCurrentUserId() => User;
}

public class AuditableInterceptorTests
{
    private (InterceptorTestDbContext, ICurrentUserService) BuildContext()
    {
        var currentUser = new CurrentUserServiceStub { User = "alice" };
        var options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("Auditable_" + Guid.NewGuid())
            .AddInterceptors(new AuditableInterceptor(currentUser))
            .Options;

        var ctx = new InterceptorTestDbContext(options);
        return (ctx, currentUser);
    }

    [Fact]
    public async Task Added_Entity_Should_Have_CreatedAt_And_CreatedBy()
    {
        var (ctx, _) = BuildContext();
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "item1");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        Assert.NotEqual(default, entity.CreatedAt);
        Assert.Equal("alice", entity.CreatedBy);
        Assert.Null(entity.UpdatedAt);
    }

    [Fact]
    public async Task Modified_Entity_Should_Have_UpdatedAt_And_UpdatedBy()
    {
        var (ctx, _) = BuildContext();
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "item1");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        var originalCreatedAt = entity.CreatedAt;
        entity.Name = "updated";
        await ctx.SaveChangesAsync();

        Assert.Equal(originalCreatedAt, entity.CreatedAt);
        Assert.NotNull(entity.UpdatedAt);
        Assert.Equal("alice", entity.UpdatedBy);
    }

    [Fact]
    public async Task Should_Not_Affect_Entities_That_Are_Not_IAuditable()
    {
        var currentUser = new CurrentUserServiceStub { User = "x" };

        var options = new DbContextOptionsBuilder<NonAuditableDbContext>()
            .UseInMemoryDatabase("NonAuditable_" + Guid.NewGuid())
            .AddInterceptors(new AuditableInterceptor(currentUser))
            .Options;

        using var ctx = new NonAuditableDbContext(options);
        ctx.Add(new NonAuditableThing { Id = Guid.NewGuid(), Value = "v" });
        await ctx.SaveChangesAsync();

        Assert.Equal("v", ctx.Things.First().Value);
    }

    private class NonAuditableDbContext : DbContext
    {
        public NonAuditableDbContext(DbContextOptions options) : base(options) { }
        public DbSet<NonAuditableThing> Things => Set<NonAuditableThing>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NonAuditableThing>().HasKey(e => e.Id);
            base.OnModelCreating(modelBuilder);
        }
    }

    private class NonAuditableThing
    {
        public Guid Id { get; set; }
        public string Value { get; set; } = "";
    }

    [Fact]
    public async Task Should_Work_Without_CurrentUserService()
    {
        var options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("NoCurrentUser_" + Guid.NewGuid())
            .AddInterceptors(new AuditableInterceptor())
            .Options;

        using var ctx = new InterceptorTestDbContext(options);
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "x");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        Assert.NotEqual(default, entity.CreatedAt);
        Assert.Null(entity.CreatedBy);
    }
}

public class SoftDeleteInterceptorTests
{
    private InterceptorTestDbContext BuildContext(ICurrentUserService? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("SoftDelete_" + Guid.NewGuid())
            .AddInterceptors(new SoftDeleteInterceptor(currentUser))
            .Options;
        return new InterceptorTestDbContext(options);
    }

    [Fact]
    public async Task Delete_Should_Set_IsDeleted_True_Instead_Of_Removing_Row()
    {
        using var ctx = BuildContext();
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "x");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        ctx.Items.Remove(entity);
        await ctx.SaveChangesAsync();

        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.DeletedAt);
    }

    [Fact]
    public async Task Delete_With_CurrentUserService_Should_Set_DeletedBy()
    {
        var currentUser = new CurrentUserServiceStub { User = "bob" };
        using var ctx = BuildContext(currentUser);

        var entity = new InterceptorTestEntity(Guid.NewGuid(), "x");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        ctx.Items.Remove(entity);
        await ctx.SaveChangesAsync();

        Assert.True(entity.IsDeleted);
        Assert.Equal("bob", entity.DeletedBy);
    }

    [Fact]
    public async Task SoftDeleted_Entity_Should_Be_Filtered_Out_By_Default()
    {
        using var ctx = BuildContext();
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "x");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        ctx.Items.Remove(entity);
        await ctx.SaveChangesAsync();

        Assert.Null(await ctx.Items.FirstOrDefaultAsync(e => e.Id == entity.Id));
    }

    [Fact]
    public async Task SoftDeleted_Entity_Should_Be_Visible_With_IgnoreQueryFilters()
    {
        using var ctx = BuildContext();
        var entity = new InterceptorTestEntity(Guid.NewGuid(), "x");
        ctx.Items.Add(entity);
        await ctx.SaveChangesAsync();

        ctx.Items.Remove(entity);
        await ctx.SaveChangesAsync();

        var found = await ctx.Items.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == entity.Id);
        Assert.NotNull(found);
        Assert.True(found!.IsDeleted);
    }
}

public class EfIdempotencyTrackerTests
{
    private class IdempotencyTestDbContext : PhantomDbContext
    {
        public IdempotencyTestDbContext(DbContextOptions options) : base(options) { }
    }

    private static IServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<IdempotencyTestDbContext>(o => o.UseInMemoryDatabase("Idempotency_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<IdempotencyTestDbContext>());
        services.AddScoped<IIdempotencyTracker, EfIdempotencyTracker>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task IsProcessedAsync_Should_Return_False_For_Unknown_Event()
    {
        var sp = BuildSp();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();

        Assert.False(await tracker.IsProcessedAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Make_Event_Reported_As_Processed()
    {
        var sp = BuildSp();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<IdempotencyTestDbContext>();

        var id = Guid.NewGuid();
        await tracker.MarkAsProcessedAsync(id, "TestEvent");
        await dbContext.SaveChangesAsync();

        Assert.True(await tracker.IsProcessedAsync(id));
    }

    [Fact]
    public async Task Multiple_Events_Should_Be_Tracked_Independently()
    {
        var sp = BuildSp();
        var tracker = sp.GetRequiredService<IIdempotencyTracker>();
        var dbContext = sp.GetRequiredService<IdempotencyTestDbContext>();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await tracker.MarkAsProcessedAsync(id1, "TestEvent1");
        await dbContext.SaveChangesAsync();

        Assert.True(await tracker.IsProcessedAsync(id1));
        Assert.False(await tracker.IsProcessedAsync(id2));
    }
}

public class OutboxMessageConfigurationTests
{
    [Fact]
    public void Configure_Should_Set_Table_Name_And_Key()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new OutboxMessageConfiguration();
        configuration.Configure(modelBuilder.Entity<OutboxMessage>());

        var entity = modelBuilder.Model.FindEntityType(typeof(OutboxMessage))!;
        Assert.Equal("OutboxMessages", entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal(nameof(OutboxMessage.Id), primaryKey!.Properties[0].Name);
    }

    [Fact]
    public void Configure_Should_Set_Max_Length_For_EventType()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new OutboxMessageConfiguration();
        configuration.Configure(modelBuilder.Entity<OutboxMessage>());

        var entity = modelBuilder.Model.FindEntityType(typeof(OutboxMessage))!;
        var eventTypeProperty = entity.FindProperty(nameof(OutboxMessage.EventType))!;
        Assert.Equal(500, eventTypeProperty.GetMaxLength());
    }

    [Fact]
    public void Configure_Should_Set_Max_Length_For_LastError()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new OutboxMessageConfiguration();
        configuration.Configure(modelBuilder.Entity<OutboxMessage>());

        var entity = modelBuilder.Model.FindEntityType(typeof(OutboxMessage))!;
        var lastErrorProperty = entity.FindProperty(nameof(OutboxMessage.LastError))!;
        Assert.Equal(2000, lastErrorProperty.GetMaxLength());
    }

    [Fact]
    public void Configure_Should_Create_Indexes()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new OutboxMessageConfiguration();
        configuration.Configure(modelBuilder.Entity<OutboxMessage>());

        var entity = modelBuilder.Model.FindEntityType(typeof(OutboxMessage))!;
        var indexedProperties = entity.GetIndexes().SelectMany(i => i.Properties.Select(p => p.Name)).ToHashSet();
        Assert.Contains(nameof(OutboxMessage.IsPublished), indexedProperties);
        Assert.Contains(nameof(OutboxMessage.CreatedAt), indexedProperties);
    }
}

public class ProcessedEventConfigurationTests
{
    [Fact]
    public void Configure_Should_Set_Table_Name_And_Key()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new ProcessedEventConfiguration();
        configuration.Configure(modelBuilder.Entity<ProcessedEvent>());

        var entity = modelBuilder.Model.FindEntityType(typeof(ProcessedEvent))!;
        Assert.Equal("ProcessedEvents", entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal(nameof(ProcessedEvent.EventId), primaryKey!.Properties[0].Name);
    }

    [Fact]
    public void Configure_Should_Set_Max_Length_For_EventType()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new ProcessedEventConfiguration();
        configuration.Configure(modelBuilder.Entity<ProcessedEvent>());

        var entity = modelBuilder.Model.FindEntityType(typeof(ProcessedEvent))!;
        var eventTypeProperty = entity.FindProperty(nameof(ProcessedEvent.EventType))!;
        Assert.Equal(500, eventTypeProperty.GetMaxLength());
    }

    [Fact]
    public void Configure_Should_Create_Index_On_EventType()
    {
        var modelBuilder = new ModelBuilder();
        var configuration = new ProcessedEventConfiguration();
        configuration.Configure(modelBuilder.Entity<ProcessedEvent>());

        var entity = modelBuilder.Model.FindEntityType(typeof(ProcessedEvent))!;
        var indexedProperties = entity.GetIndexes().SelectMany(i => i.Properties.Select(p => p.Name)).ToHashSet();
        Assert.Contains(nameof(ProcessedEvent.EventType), indexedProperties);
    }
}

public class PhantomDataOptionsTests
{
    [Fact]
    public void Validate_With_InMemory_Provider_Should_Not_Require_ConnectionString()
    {
        var options = new PhantomDataOptions
        {
            Provider = DatabaseProvider.InMemory
        };
        options.Validate();
    }

    [Fact]
    public void Validate_With_PostgreSQL_And_No_ConnectionString_Should_Throw()
    {
        var options = new PhantomDataOptions
        {
            Provider = DatabaseProvider.PostgreSQL
        };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_With_SqlServer_And_No_ConnectionString_Should_Throw()
    {
        var options = new PhantomDataOptions
        {
            Provider = DatabaseProvider.SqlServer
        };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_With_Custom_ConfigureDbContext_Should_Pass_Even_Without_ConnectionString()
    {
        var options = new PhantomDataOptions
        {
            Provider = DatabaseProvider.PostgreSQL,
            ConfigureDbContext = _ => { }
        };
        options.Validate();
    }

    [Fact]
    public void Defaults_Should_Be_Sensible()
    {
        var options = new PhantomDataOptions();
        Assert.Equal(DatabaseProvider.PostgreSQL, options.Provider);
        Assert.True(options.UseOutbox);
        Assert.False(options.UseIdempotency);
        Assert.False(options.UseSoftDelete);
        Assert.False(options.UseAuditable);
        Assert.Null(options.ConnectionString);
    }
}

public class DomainEventOutboxDispatcherTests
{
    private class OutboxTestEvent : DomainEvent
    {
        public string Data { get; }
        public OutboxTestEvent(string data) { Data = data; }
    }

    private class OutboxTestAggregate : AggregateRoot<Guid>
    {
        public OutboxTestAggregate(Guid id) : base(id) { }

        public void Raise(string data) => AddDomainEvent(new OutboxTestEvent(data));
    }

    private class OutboxTestDbContext : PhantomDbContext
    {
        public OutboxTestDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null, IMessageSerializer? messageSerializer = null)
            : base(options, dispatcher, null, messageSerializer) { }

        public DbSet<OutboxTestAggregate> Aggregates => Set<OutboxTestAggregate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OutboxTestAggregate>(e =>
            {
                e.HasKey(a => a.Id);
                e.Ignore(a => a.DomainEvents);
            });
        }
    }

    [Fact]
    public async Task SaveChanges_With_MessageSerializer_Should_Enqueue_OutboxMessages()
    {
        var serializer = new Phantom.Messaging.Abstractions.JsonMessageSerializer();
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase("OutboxDispatch_" + Guid.NewGuid())
            .Options;

        using var ctx = new OutboxTestDbContext(options, messageSerializer: serializer);
        var aggregate = new OutboxTestAggregate(Guid.NewGuid());
        aggregate.Raise("event1");
        aggregate.Raise("event2");
        ctx.Aggregates.Add(aggregate);

        await ctx.SaveChangesAsync();

        var outboxMessages = await ctx.Set<OutboxMessage>().ToListAsync();
        Assert.Equal(2, outboxMessages.Count);
        Assert.All(outboxMessages, m => Assert.False(m.IsPublished));
        Assert.All(outboxMessages, m => Assert.False(string.IsNullOrEmpty(m.Payload)));
        Assert.All(outboxMessages, m => Assert.False(string.IsNullOrEmpty(m.EventType)));
    }

    [Fact]
    public async Task SaveChanges_With_MessageSerializer_Should_Not_Dispatch_InProcess()
    {
        var inProcessDispatcher = new ThrowingDispatcherForOutbox(failOnNth: 1);
        var serializer = new Phantom.Messaging.Abstractions.JsonMessageSerializer();
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase("OutboxNoInProcess_" + Guid.NewGuid())
            .Options;

        using var ctx = new OutboxTestDbContext(options, inProcessDispatcher, messageSerializer: serializer);
        var aggregate = new OutboxTestAggregate(Guid.NewGuid());
        aggregate.Raise("event1");
        ctx.Aggregates.Add(aggregate);

        await ctx.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void UseOutboxForDomainEvents_Should_Be_True_When_MessageSerializer_Provided()
    {
        var serializer = new Phantom.Messaging.Abstractions.JsonMessageSerializer();
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase("OutboxFlag_" + Guid.NewGuid())
            .Options;

        using var ctx = new OutboxTestDbContext(options, messageSerializer: serializer);
        Assert.True(ctx.UseOutboxForDomainEvents);
    }

    [Fact]
    public void UseOutboxForDomainEvents_Should_Be_False_When_No_MessageSerializer()
    {
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase("NoOutbox_" + Guid.NewGuid())
            .Options;

        using var ctx = new OutboxTestDbContext(options);
        Assert.False(ctx.UseOutboxForDomainEvents);
    }

    private class ThrowingDispatcherForOutbox : IDomainEventDispatcher
    {
        private readonly int _failOnNth;
        private int _count;
        public ThrowingDispatcherForOutbox(int failOnNth) { _failOnNth = failOnNth; }

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            _count++;
            if (_count == _failOnNth)
                throw new InvalidOperationException("Handler failed");
            return Task.CompletedTask;
        }

        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (var e in domainEvents)
                DispatchAsync(e, cancellationToken);
            return Task.CompletedTask;
        }
    }
}

public class PhantomDbContextGlobalQueryFilterTests
{
    private class FilterTestDbContext : PhantomDbContext
    {
        public FilterTestDbContext(DbContextOptions options) : base(options) { }
        public DbSet<InterceptorTestEntity> Items => Set<InterceptorTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<InterceptorTestEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired();
            });
        }
    }

    [Fact]
    public async Task Global_Query_Filter_Should_Exclude_SoftDeleted_Entities_Even_Without_Interceptor()
    {
        var options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("Filter_" + Guid.NewGuid())
            .Options;

        using var ctx = new FilterTestDbContext(options);
        var active = new InterceptorTestEntity(Guid.NewGuid(), "active");
        var deleted = new InterceptorTestEntity(Guid.NewGuid(), "deleted");
        deleted.SoftDelete("tester");
        ctx.Items.AddRange(active, deleted);
        await ctx.SaveChangesAsync();

        var visible = await ctx.Items.ToListAsync();
        Assert.Single(visible);
        Assert.Equal(active.Id, visible[0].Id);
    }
}

public class EfSpecificationEvaluatorAdvancedTests
{
    private class SpecEvalEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    private class SpecEvalDbContext : DbContext
    {
        public SpecEvalDbContext(DbContextOptions options) : base(options) { }
        public DbSet<SpecEvalEntity> Items => Set<SpecEvalEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SpecEvalEntity>().HasKey(e => e.Id);
            base.OnModelCreating(modelBuilder);
        }
    }

    private class NameStartsWithSpec : QuerySpecification<SpecEvalEntity>
    {
        public NameStartsWithSpec(string prefix)
        {
            ApplyOrderBy(e => e.Name);
            ApplyPaging(0, 5);
            ApplyAsNoTracking();
        }

        public override bool IsSatisfiedBy(SpecEvalEntity candidate) => true;
        public override Expression<Func<SpecEvalEntity, bool>> ToExpression() => e => e.Name != "";
    }

    [Fact]
    public void ApplySpecification_With_Null_Specification_Should_Throw()
    {
        var evaluator = new EfSpecificationEvaluator();
        using var ctx = new SpecEvalDbContext(
            new DbContextOptionsBuilder<SpecEvalDbContext>()
                .UseInMemoryDatabase("SpecEvalNull_" + Guid.NewGuid()).Options);

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.ApplySpecification(ctx.Items.AsQueryable(), null!));
    }

    [Fact]
    public async Task ApplySpecification_Should_Apply_Paging_And_Ordering()
    {
        var evaluator = new EfSpecificationEvaluator();
        using var ctx = new SpecEvalDbContext(
            new DbContextOptionsBuilder<SpecEvalDbContext>()
                .UseInMemoryDatabase("SpecEvalPaged_" + Guid.NewGuid()).Options);

        for (int i = 0; i < 10; i++)
            ctx.Items.Add(new SpecEvalEntity { Id = Guid.NewGuid(), Name = $"Item-{i:D2}", Price = i });

        await ctx.SaveChangesAsync();

        var spec = new NameStartsWithSpec("Item-");
        var results = await evaluator.ApplySpecification(ctx.Items.AsQueryable(), spec).ToListAsync();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void ApplySpecification_Should_Apply_NoTracking_When_Requested()
    {
        var evaluator = new EfSpecificationEvaluator();
        using var ctx = new SpecEvalDbContext(
            new DbContextOptionsBuilder<SpecEvalDbContext>()
                .UseInMemoryDatabase("SpecEvalNoTracking_" + Guid.NewGuid()).Options);

        var spec = new NameStartsWithSpec("X");
        var query = evaluator.ApplySpecification(ctx.Items.AsQueryable(), spec);

        Assert.True(query.AsNoTracking() is IQueryable<SpecEvalEntity>);
    }
}

public class RepositoryFirstOrDefaultTests
{
    private class FpEntity : Entity<Guid>
    {
        public string Name { get; set; } = "";
        public FpEntity() { }
        public FpEntity(Guid id, string name) : base(id) { Name = name; }
    }

    private class FpDbContext : PhantomDbContext
    {
        public FpDbContext(DbContextOptions options) : base(options) { }
        public DbSet<FpEntity> Items => Set<FpEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<FpEntity>().HasKey(e => e.Id);
        }
    }

    private class NameSpec : Specification<FpEntity>
    {
        private readonly string _name;
        public NameSpec(string name) { _name = name; }
        public override bool IsSatisfiedBy(FpEntity candidate) => candidate.Name == _name;
        public override Expression<Func<FpEntity, bool>> ToExpression() => e => e.Name == _name;
    }

    private static IServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FpDbContext>(o => o.UseInMemoryDatabase("Fp_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<FpDbContext>());
        services.AddSingleton<ISpecificationEvaluator, EfSpecificationEvaluator>();
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Should_Return_First_Matching_Entity()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IRepository<Guid, FpEntity>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new FpEntity(id, "match"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "other"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var result = await repo.FirstOrDefaultAsync(new NameSpec("match"));
        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Should_Return_Null_When_No_Match()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IRepository<Guid, FpEntity>>();

        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "x"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var result = await repo.FirstOrDefaultAsync(new NameSpec("does-not-exist"));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsNoTrackingAsync_Should_Return_Entity_Without_Tracking()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IRepository<Guid, FpEntity>>();
        var id = Guid.NewGuid();

        await repo.AddAsync(new FpEntity(id, "x"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var entity = await repo.GetByIdAsNoTrackingAsync(id);
        Assert.NotNull(entity);
        Assert.Equal(id, entity!.Id);
    }

    [Fact]
    public async Task FindAsNoTrackingAsync_Should_Return_Results_Without_Tracking()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IRepository<Guid, FpEntity>>();

        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "match"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "match"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "other"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var results = await repo.FindAsNoTrackingAsync(new NameSpec("match"));
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Entities()
    {
        var sp = BuildSp();
        var repo = (Repository<Guid, FpEntity>)sp.GetRequiredService<IRepository<Guid, FpEntity>>();

        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "a"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "b"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "c"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var all = await repo.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetAllAsNoTrackingAsync_Should_Return_All_Entities_Without_Tracking()
    {
        var sp = BuildSp();
        var repo = (Repository<Guid, FpEntity>)sp.GetRequiredService<IRepository<Guid, FpEntity>>();

        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "a"));
        await repo.AddAsync(new FpEntity(Guid.NewGuid(), "b"));
        await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        var all = await repo.GetAllAsNoTrackingAsync();
        Assert.Equal(2, all.Count);
    }
}

public class EfOutboxRepositoryGetFailedTests
{
    private class GetFailedDbContext : PhantomDbContext
    {
        public GetFailedDbContext(DbContextOptions options) : base(options) { }
    }

    private static IServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<GetFailedDbContext>(o => o.UseInMemoryDatabase("GetFailed_" + Guid.NewGuid()));
        services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<GetFailedDbContext>());
        services.AddScoped<IOutboxMessageRepository, EfOutboxRepository>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetFailedAsync_Should_Return_Failed_Messages_Below_MaxRetry()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        var msg1 = new OutboxMessage { EventType = "Type1", Payload = "{}", Channel = "default", LastError = "boom", RetryCount = 1 };
        var msg2 = new OutboxMessage { EventType = "Type1", Payload = "{}", Channel = "default", LastError = "boom", RetryCount = 11 };
        var msg3 = new OutboxMessage { EventType = "Type1", Payload = "{}", Channel = "default", LastError = null, RetryCount = 0 };

        await repo.AddAsync(msg1);
        await repo.AddAsync(msg2);
        await repo.AddAsync(msg3);
        await dbContext.SaveChangesAsync();

        var failed = await repo.GetFailedAsync(maxRetryCount: 10, batchSize: 100);

        Assert.Single(failed);
        Assert.Equal(msg1.Id, failed[0].Id);
    }

    [Fact]
    public async Task GetFailedAsync_Should_Respect_Batch_Size()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        for (int i = 0; i < 5; i++)
            await repo.AddAsync(new OutboxMessage { EventType = "T", Payload = "{}", Channel = "default", LastError = "boom" });
        await dbContext.SaveChangesAsync();

        var failed = await repo.GetFailedAsync(maxRetryCount: 10, batchSize: 2);
        Assert.Equal(2, failed.Count);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Respect_Batch_Size()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        for (int i = 0; i < 10; i++)
            await repo.AddAsync(new OutboxMessage { EventType = "T", Payload = "{}", Channel = "default" });
        await dbContext.SaveChangesAsync();

        var pending = await repo.GetPendingAsync(batchSize: 3);
        Assert.Equal(3, pending.Count);
    }

    [Fact]
    public async Task MarkAsPublishedAsync_Should_Set_PublishedAt_Timestamp()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        var msg = new OutboxMessage { EventType = "T", Payload = "{}", Channel = "default" };
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await repo.MarkAsPublishedAsync(msg.Id);
        await dbContext.SaveChangesAsync();

        var fetched = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsPublished);
        Assert.NotNull(fetched.PublishedAt);
        Assert.True(fetched.PublishedAt > before);
    }

    [Fact]
    public async Task MarkAsFailedAsync_With_Unknown_Id_Should_Be_NoOp()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        await repo.MarkAsFailedAsync(Guid.NewGuid(), "no such message");
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Increment_And_Store_Error()
    {
        var sp = BuildSp();
        var repo = sp.GetRequiredService<IOutboxMessageRepository>();
        var dbContext = sp.GetRequiredService<GetFailedDbContext>();

        var msg = new OutboxMessage { EventType = "T", Payload = "{}", Channel = "default" };
        await repo.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        await repo.IncrementRetryCountAsync(msg.Id, "first failure");
        await dbContext.SaveChangesAsync();
        await repo.IncrementRetryCountAsync(msg.Id, "second failure");
        await dbContext.SaveChangesAsync();

        var fetched = await dbContext.Set<OutboxMessage>().FindAsync(msg.Id);
        Assert.Equal(2, fetched!.RetryCount);
        Assert.Equal("second failure", fetched.LastError);
    }
}

public class AddPhantomDataIntegrationTests
{
    private class PhantomDataTestDbContext : PhantomDbContext
    {
        public PhantomDataTestDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
            : base(options, dispatcher) { }
    }

    [Fact]
    public void AddPhantomData_InMemory_Should_Register_Required_Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomData(d =>
        {
            d.Provider = DatabaseProvider.InMemory;
            d.UseOutbox = true;
            d.UseIdempotency = true;
        });

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISpecificationEvaluator>());
        Assert.NotNull(sp.GetService<IUnitOfWork>());
        Assert.NotNull(sp.GetService<IDomainEventDispatcher>());
        Assert.NotNull(sp.GetService<IOutboxMessageRepository>());
        Assert.NotNull(sp.GetService<IIdempotencyTracker>());
        Assert.NotNull(sp.GetService<IRepository<Guid, SomeEntity>>());
    }

    [Fact]
    public void AddPhantomData_Should_Register_ICurrentUserService_Default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomData(d =>
        {
            d.Provider = DatabaseProvider.InMemory;
            d.UseOutbox = false;
        });

        var sp = services.BuildServiceProvider();
        var currentUser = sp.GetService<ICurrentUserService>();
        Assert.NotNull(currentUser);
        Assert.Null(currentUser!.GetCurrentUserId());
    }

    private class SomeEntity : Entity<Guid>
    {
        public SomeEntity() { }
        public SomeEntity(Guid id) : base(id) { }
    }
}
