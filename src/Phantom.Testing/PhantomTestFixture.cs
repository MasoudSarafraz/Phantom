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

namespace Phantom.Testing;

public class PhantomTestFixture : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<IIntegrationEvent> _publishedEvents = new();
    private readonly object _publishedEventsLock = new();

    public PhantomTestFixture(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IServiceProvider Services => _serviceProvider;

    public IServiceProvider CreateScope()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider;
    }

    public T GetRequiredService<T>() where T : notnull =>
        _serviceProvider.GetRequiredService<T>();

    public T? GetService<T>() =>
        _serviceProvider.GetService<T>();

    public IDispatcher GetDispatcher() => _serviceProvider.GetRequiredService<IDispatcher>();

    public TDbContext GetDbContext<TDbContext>() where TDbContext : DbContext =>
        _serviceProvider.GetRequiredService<TDbContext>();

    public IReadOnlyList<IIntegrationEvent> GetPublishedEvents()
    {
        lock (_publishedEventsLock) { return _publishedEvents.ToList(); }
    }

    public IReadOnlyList<T> GetPublishedEvents<T>() where T : IIntegrationEvent
    {
        lock (_publishedEventsLock) { return _publishedEvents.OfType<T>().ToList(); }
    }

    public void ClearPublishedEvents()
    {
        lock (_publishedEventsLock) { _publishedEvents.Clear(); }
    }

    internal void RecordPublishedEvent(IIntegrationEvent @event)
    {
        lock (_publishedEventsLock) { _publishedEvents.Add(@event); }
    }

    public async Task<IDisposable> BeginScopeAsync()
    {
        var scope = _serviceProvider.CreateAsyncScope();
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }
}

public class PhantomTestScope : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    private readonly PhantomTestFixture _fixture;

    public PhantomTestScope(PhantomTestFixture fixture, AsyncServiceScope scope)
    {
        _fixture = fixture;
        _scope = scope;
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public IDispatcher Dispatcher => _scope.ServiceProvider.GetRequiredService<IDispatcher>();

    public TDbContext DbContext<TDbContext>() where TDbContext : DbContext =>
        _scope.ServiceProvider.GetRequiredService<TDbContext>();

    public IRepository<TId, TEntity> Repository<TId, TEntity>() where TEntity : Phantom.Core.Domain.Entity<TId> where TId : notnull =>
        _scope.ServiceProvider.GetRequiredService<IRepository<TId, TEntity>>();

    public IUnitOfWork UnitOfWork => _scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    public IReadOnlyList<IIntegrationEvent> PublishedEvents => _fixture.GetPublishedEvents();

    public IReadOnlyList<T> PublishedEventsOf<T>() where T : IIntegrationEvent => _fixture.GetPublishedEvents<T>();

    public void ClearPublishedEvents() => _fixture.ClearPublishedEvents();

    public async ValueTask DisposeAsync() => await _scope.DisposeAsync();
}

public static class PhantomTestFixtureExtensions
{
    public static PhantomTestScope CreateTestScope(this PhantomTestFixture fixture)
    {
        var scope = fixture.Services.CreateAsyncScope();
        return new PhantomTestScope(fixture, scope);
    }
}
