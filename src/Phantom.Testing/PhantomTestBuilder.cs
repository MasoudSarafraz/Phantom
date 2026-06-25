using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.Core.Events;
using Phantom.Core.Messaging;
using Phantom.Core.Services;
using Phantom.CQRS.Dispatchers;
using Phantom.Data.EfCore;
using Phantom.Data.Extensions;
using Phantom.Data.Specifications;
using Phantom.Infrastructure.Abstractions.Idempotency;
using Phantom.Infrastructure.Abstractions.Outbox;
using Phantom.Messaging.Abstractions;
using Phantom.Messaging.InMemory;
using Phantom.NET.Extensions;
using System.Reflection;

namespace Phantom.Testing;

public class PhantomTestBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly List<Assembly> _assemblies = new();
    private string _databaseName = "PhantomTestDb";
    private bool _useInMemoryBroker = true;
    private bool _useRealOutbox = false;
    private bool _useRealIdempotency = false;
    private bool _capturePublishedEvents = true;
    private bool _useSoftDelete = false;
    private bool _useAuditable = false;
    private bool _useFluentValidation = false;
    private Action<DbContextOptionsBuilder>? _dbContextConfigurator = null;
    private Type? _dbContextType;

    public PhantomTestBuilder WithAssembliesFrom(params Assembly[] assemblies)
    {
        _assemblies.AddRange(assemblies);
        return this;
    }

    public PhantomTestBuilder WithAssembliesFrom(params Type[] types)
    {
        _assemblies.AddRange(types.Select(t => t.Assembly));
        return this;
    }

    public PhantomTestBuilder WithDatabaseName(string name)
    {
        _databaseName = name;
        return this;
    }

    public PhantomTestBuilder WithInMemoryBroker()
    {
        _useInMemoryBroker = true;
        return this;
    }

    public PhantomTestBuilder WithSoftDelete()
    {
        _useSoftDelete = true;
        return this;
    }

    public PhantomTestBuilder WithAuditable()
    {
        _useAuditable = true;
        return this;
    }

    public PhantomTestBuilder WithFluentValidation()
    {
        _useFluentValidation = true;
        return this;
    }

    public PhantomTestBuilder WithRealOutbox()
    {
        _useRealOutbox = true;
        return this;
    }

    public PhantomTestBuilder WithRealIdempotency()
    {
        _useRealIdempotency = true;
        return this;
    }

    public PhantomTestBuilder WithCapturePublishedEvents(bool enabled = true)
    {
        _capturePublishedEvents = enabled;
        return this;
    }

    public PhantomTestBuilder WithDbContext<TDbContext>() where TDbContext : PhantomDbContext
    {
        _dbContextType = typeof(TDbContext);
        return this;
    }

    public PhantomTestBuilder WithDbContextConfigurator(Action<DbContextOptionsBuilder> configurator)
    {
        _dbContextConfigurator = configurator;
        return this;
    }

    public PhantomTestFixture Build()
    {
        _services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        if (_assemblies.Count == 0)
        {
            _services.AddPhantom(Array.Empty<Assembly>(), options =>
            {
                ConfigureOptions(options);
            });
        }
        else
        {
            _services.AddPhantom(_assemblies.ToArray(), options =>
            {
                ConfigureOptions(options);
            });
        }

        if (_dbContextType is not null)
        {
            var addDbContextMethod = typeof(EntityFrameworkServiceCollectionExtensions)
                .GetMethods()
                .Where(m => m.Name == nameof(EntityFrameworkServiceCollectionExtensions.AddDbContext) && m.IsGenericMethod)
                .Select(m => new { Method = m, Params = m.GetParameters() })
                .Where(x => x.Params.Length == 2 && x.Params[1].ParameterType == typeof(Action<DbContextOptionsBuilder>))
                .Select(x => x.Method)
                .First()
                .MakeGenericMethod(_dbContextType);

            addDbContextMethod.Invoke(null, new object[] { _services, (Action<DbContextOptionsBuilder>)(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                _dbContextConfigurator?.Invoke(options);
            }) });

            _services.AddScoped<PhantomDbContext>(sp => (PhantomDbContext)sp.GetRequiredService(_dbContextType));
        }
        else
        {
            _services.AddDbContext<PhantomTestDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                _dbContextConfigurator?.Invoke(options);
            });
            _services.AddScoped<PhantomDbContext>(sp => sp.GetRequiredService<PhantomTestDbContext>());
        }

        if (_capturePublishedEvents)
        {
            _services.Decorate<IEventPublisher, CapturingEventPublisher>();
        }

        _services.AddSingleton<ICurrentUserService, TestCurrentUserService>();

        var provider = _services.BuildServiceProvider();
        return new PhantomTestFixture(provider);
    }

    private void ConfigureOptions(Phantom.NET.Extensions.PhantomOptions options)
    {
        options.UseInMemoryDatabase(d =>
        {
            d.UseSoftDelete = _useSoftDelete;
            d.UseAuditable = _useAuditable;
            d.UseOutbox = _useRealOutbox;
            d.UseIdempotency = _useRealIdempotency;
        });

        if (_useFluentValidation)
        {
            options.UseFluentValidation();
        }

        if (_useInMemoryBroker)
        {
            options.AddChannel("test", c => c.UseInMemory());
        }

        if (_useRealOutbox)
        {
            options.UseOutbox();
        }

        if (_useRealIdempotency)
        {
            options.EnableIdempotency();
        }
    }

    public async Task<PhantomTestFixture> BuildAsync()
    {
        var fixture = Build();
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<PhantomDbContext>();
        if (dbContext is not null)
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        return fixture;
    }
}

public class CapturingEventPublisher : IEventPublisher
{
    private readonly IEventPublisher _inner;
    private readonly PhantomTestFixture _fixture;

    public CapturingEventPublisher(IEventPublisher inner, PhantomTestFixture fixture)
    {
        _inner = inner;
        _fixture = fixture;
    }

    public Task PublishAsync<TEvent>(TEvent @event, string channel, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        _fixture.RecordPublishedEvent(@event);
        return _inner.PublishAsync(@event, channel, ct);
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        _fixture.RecordPublishedEvent(@event);
        return _inner.PublishAsync(@event, ct);
    }
}

public class TestCurrentUserService : ICurrentUserService
{
    public string? CurrentUserId { get; set; } = "test-user";

    public string? GetCurrentUserId() => CurrentUserId;
}

public class PhantomTestDbContext : PhantomDbContext
{
    public PhantomTestDbContext(DbContextOptions options) : base(options) { }
}
