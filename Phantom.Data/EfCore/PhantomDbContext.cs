using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace Phantom.Data.EfCore;

public class PhantomDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;
    private readonly ILogger<PhantomDbContext>? _logger;

    public PhantomDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
        _logger = logger;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregateRoots = ChangeTracker.Entries()
            .Where(e => e.Entity is IAggregateRoot)
            .Select(e => (IAggregateRoot)e.Entity)
            .ToList();

        var domainEvents = aggregateRoots
            .SelectMany(ar => ar.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_domainEventDispatcher != null)
        {
            foreach (var domainEvent in domainEvents)
            {
                try
                {
                    await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "[Phantom] Failed to dispatch domain event {EventType}. Continuing with remaining events.",
                        domainEvent.GetType().Name);
                }
            }
        }

        foreach (var aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
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
