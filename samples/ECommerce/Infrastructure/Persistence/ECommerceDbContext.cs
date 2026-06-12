using ECommerce.Domain.Entities;
using Phantom.Data.EfCore;

namespace ECommerce.Infrastructure.Persistence;

public class ECommerceDbContext : PhantomDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();

    public ECommerceDbContext(
        DbContextOptions options,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ILogger<PhantomDbContext>? logger = null)
        : base(options, domainEventDispatcher, logger) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers");
            b.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
            b.Property(c => c.LastName).HasMaxLength(100).IsRequired();
            b.OwnsOne(c => c.Email, e => e.Property(x => x.Value).HasColumnName("Email").HasMaxLength(256).IsRequired());
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.OwnsOne(p => p.Price, p =>
            {
                p.Property(x => x.Amount).HasColumnName("PriceAmount").HasColumnType("decimal(18,2)");
                p.Property(x => x.Currency).HasColumnName("PriceCurrency").HasMaxLength(3);
            });
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.Property(o => o.Status).HasMaxLength(50).IsRequired();
            b.Property(o => o.ShippingAddress).HasMaxLength(500).IsRequired();
            b.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.ToTable("OrderLines");
            b.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
            b.OwnsOne(l => l.UnitPrice, p =>
            {
                p.Property(x => x.Amount).HasColumnName("UnitPriceAmount").HasColumnType("decimal(18,2)");
                p.Property(x => x.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
            });
        });
    }
}
