using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApp.Domain.Entities;
using Phantom.Core.Events;
using Phantom.Data.EfCore;

namespace MyApp.Infrastructure.Persistence;

public class MyAppDbContext : PhantomDbContext
{
    public DbSet<Product> Products => Set<Product>();

    public MyAppDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null)
        : base(options, domainEventDispatcher, logger) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Price).HasColumnType("decimal(18,2)");
            b.Ignore(p => p.DomainEvents);
        });
    }
}
