using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Services;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.EfCore;

public class PhantomDbContext : DbContext
{
    private readonly DomainEventOutboxDispatcher? _dispatcher;
    private readonly ILogger<PhantomDbContext>? _logger;

    public PhantomDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null,
        IMessageSerializer? messageSerializer = null)
        : base(options)
    {
        _logger = logger;

        if (domainEventDispatcher is not null || messageSerializer is not null)
        {
            _dispatcher = new DomainEventOutboxDispatcher(
                domainEventDispatcher,
                messageSerializer,
                logger: null);
        }
    }

    public bool UseOutboxForDomainEvents => _dispatcher?.UseOutboxForDomainEvents ?? false;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(IAggregateRoot Aggregate, IReadOnlyList<IDomainEvent> Events)>? collected = null;

        try
        {
            collected = _dispatcher is null
                ? Array.Empty<(IAggregateRoot, IReadOnlyList<IDomainEvent>)>()
                : _dispatcher.CollectAndEnqueueOutbox(this);
        }
        catch (Exception collectEx)
        {
            _logger?.LogError(collectEx,
                "[Phantom] Failed to collect domain events before SaveChanges. SaveChanges will proceed but domain events may be lost.");
            collected = Array.Empty<(IAggregateRoot, IReadOnlyList<IDomainEvent>)>();
        }

        int result;
        try
        {
            result = await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            foreach (var (aggregate, _) in collected!)
            {
                try
                {
                    ((IAggregateRootPersistence)aggregate).ClearDomainEvents();
                }
                catch (Exception clearEx)
                {
                    _logger?.LogWarning(clearEx,
                        "[Phantom] Failed to clear domain events for aggregate {AggregateType} after SaveChanges failure.",
                        aggregate.GetType().Name);
                }
            }
            throw;
        }

        if (_dispatcher is not null && collected.Count > 0)
        {
            try
            {
                await _dispatcher.AfterSaveChangesAsync(collected, cancellationToken);
            }
            catch (Exception afterSaveEx)
            {
                _logger?.LogError(afterSaveEx,
                    "[Phantom] Failed to dispatch domain events after SaveChanges. Data was persisted successfully but domain event handlers may not have run.");
            }
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PhantomDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
