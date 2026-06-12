using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace Phantom.Data.EfCore;

public interface ISoftDeletable { bool IsDeleted { get; } }

public class PhantomDbContext : DbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public PhantomDbContext(DbContextOptions options, IDomainEventDispatcher? domainEventDispatcher = null) : base(options) { _domainEventDispatcher = domainEventDispatcher; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregateRoots = ChangeTracker.Entries()
            .Where(e => e.Entity is AggregateRoot<Guid> || e.Entity is AggregateRoot<int> || e.Entity is AggregateRoot<string>)
            .Select(e => e.Entity).ToList();

        var domainEvents = new List<IDomainEvent>();
        foreach (var entity in aggregateRoots) { var events = (IReadOnlyCollection<IDomainEvent>)((dynamic)entity).DomainEvents; domainEvents.AddRange(events); }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_domainEventDispatcher != null) foreach (var domainEvent in domainEvents) await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        foreach (var entity in aggregateRoots) { ((dynamic)entity).ClearDomainEvents(); }
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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
