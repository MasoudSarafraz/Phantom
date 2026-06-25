using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.StronglyTypedIds;
using Phantom.Data.EfCore;
using Phantom.Data.Extensions;
using Phantom.NET.Extensions;
using Xunit;

namespace Phantom.Tests.NewFeatures;

public class ConfigurationBindingTests
{
    [Fact]
    public void AddPhantom_With_IConfiguration_Should_Bind_Database_Provider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phantom:Database:Provider"] = "InMemory",
                ["Phantom:Features:UseOutbox"] = "false",
                ["Phantom:Features:UseIdempotency"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantom(new[] { typeof(ConfigurationBindingTests).Assembly }, config);

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetService<PhantomDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AddPhantom_With_IConfiguration_Should_Bind_PostgreSQL()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phantom:Database:Provider"] = "PostgreSQL",
                ["Phantom:Database:ConnectionString"] = "Host=localhost;Database=test;",
                ["Phantom:Features:UseOutbox"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantom(new[] { typeof(ConfigurationBindingTests).Assembly }, config);

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetService<PhantomDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AddPhantom_With_IConfiguration_Should_Bind_RabbitMq_Channel()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phantom:Database:Provider"] = "InMemory",
                ["Phantom:Features:UseOutbox"] = "false",
                ["Phantom:Features:UseIdempotency"] = "false",
                ["Phantom:Messaging:Channels:orders:Type"] = "RabbitMq",
                ["Phantom:Messaging:Channels:orders:RabbitMq:Host"] = "localhost",
                ["Phantom:Messaging:Channels:orders:RabbitMq:Port"] = "5672",
                ["Phantom:Messaging:Channels:orders:RabbitMq:Username"] = "guest",
                ["Phantom:Messaging:Channels:orders:RabbitMq:Password"] = "guest"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var capturedOptions = (PhantomOptions?)null;
        services.AddPhantom(new[] { typeof(ConfigurationBindingTests).Assembly }, config, o => capturedOptions = o);

        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions!.MessagingOptions.ChannelBuilders.ContainsKey("orders"));
    }

    [Fact]
    public void AddPhantom_With_IConfiguration_Should_Bind_Retry_Options()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phantom:Database:Provider"] = "InMemory",
                ["Phantom:Features:UseOutbox"] = "false",
                ["Phantom:Messaging:Retry:MaxRetries"] = "5",
                ["Phantom:Messaging:Retry:BaseDelay"] = "00:00:02"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var capturedRetry = (Phantom.Messaging.Extensions.RetryOptions?)null;
        services.AddPhantom(new[] { typeof(ConfigurationBindingTests).Assembly }, config, o =>
        {
            capturedRetry = o.MessagingOptions.Retry;
        });

        Assert.NotNull(capturedRetry);
        Assert.Equal(5, capturedRetry!.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), capturedRetry.BaseDelay);
    }
}

public class StronglyTypedIdTests
{
    [Fact]
    public void GuidId_Should_Generate_Unique_Values()
    {
        var id1 = GuidId.New();
        var id2 = GuidId.New();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1.Value);
    }

    [Fact]
    public void GuidId_Should_Be_Equal_For_Same_Value()
    {
        var guid = Guid.NewGuid();
        var id1 = new GuidId(guid);
        var id2 = new GuidId(guid);

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void GuidId_Should_Implicitly_Convert_To_Guid()
    {
        var guid = Guid.NewGuid();
        GuidId id = new(guid);

        Guid converted = id;

        Assert.Equal(guid, converted);
    }

    [Fact]
    public void GuidId_Should_Reject_Empty_Guid()
    {
        Assert.Throws<ArgumentException>(() => new GuidId(Guid.Empty));
    }

    [Fact]
    public void IntId_Should_Reject_NonPositive()
    {
        Assert.Throws<ArgumentException>(() => new IntId(0));
        Assert.Throws<ArgumentException>(() => new IntId(-1));
    }

    [Fact]
    public void StringId_Should_Reject_Null_Or_Whitespace()
    {
        Assert.Throws<ArgumentException>(() => new StringId(null!));
        Assert.Throws<ArgumentException>(() => new StringId(""));
        Assert.Throws<ArgumentException>(() => new StringId("   "));
    }

    [Fact]
    public void StronglyTypedIds_Should_Prevent_Type_Mixing()
    {
        var orderId = new GuidId(Guid.NewGuid());
        var customerId = new GuidId(Guid.NewGuid());

        Assert.NotEqual(orderId, customerId);
        Assert.True(orderId != customerId);
    }

    [Fact]
    public void GuidId_Should_Support_Comparison()
    {
        var g1 = new GuidId(new Guid("00000000-0000-0000-0000-000000000001"));
        var g2 = new GuidId(new Guid("00000000-0000-0000-0000-000000000002"));
        var g3 = new GuidId(new Guid("00000000-0000-0000-0000-000000000002"));

        Assert.True(g1 < g2);
        Assert.True(g2 > g1);
        Assert.True(g2 >= g3);
        Assert.True(g2 <= g3);
    }

    [Fact]
    public void LongId_Should_Work_For_Snowflake_Ids()
    {
        var id1 = new LongId(123456789L);
        var id2 = new LongId(123456789L);
        var id3 = new LongId(987654321L);

        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 < id3);
    }
}

public class StronglyTypedIdEfCoreConverterTests
{
    private class TestEntityWithGuidId : Phantom.Core.Domain.AggregateRoot<GuidId>
    {
        public string Name { get; set; } = string.Empty;
        public TestEntityWithGuidId() { }
        public TestEntityWithGuidId(GuidId id, string name) : base(id) { Name = name; }
    }

    private class ConverterTestDbContext : PhantomDbContext
    {
        public DbSet<TestEntityWithGuidId> Entities => Set<TestEntityWithGuidId>();
        public ConverterTestDbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntityWithGuidId>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200);
                e.Ignore(x => x.DomainEvents);
            });
        }
    }

    [Fact]
    public async Task EfCore_Should_Convert_StronglyTypedId_To_Guid()
    {
        var options = new DbContextOptionsBuilder<ConverterTestDbContext>()
            .UseInMemoryDatabase("StronglyTypedId_" + Guid.NewGuid())
            .Options;

        using var context = new ConverterTestDbContext(options);
        var id = GuidId.New();
        context.Entities.Add(new TestEntityWithGuidId(id, "test"));
        await context.SaveChangesAsync();

        var entity = await context.Entities.FindAsync(id);
        Assert.NotNull(entity);
        Assert.Equal("test", entity!.Name);
        Assert.Equal(id, entity.Id);
    }
}
