using Microsoft.EntityFrameworkCore;
using Wms.Domain;

namespace Wms.Infrastructure;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Sku).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.HasIndex(l => l.BinCode).IsUnique();
            e.Property(l => l.BinCode).HasMaxLength(16);
            e.Property(l => l.Zone).HasConversion<string>().HasMaxLength(8);
        });

        modelBuilder.Entity<StockLevel>(e =>
        {
            e.HasIndex(s => new { s.ProductId, s.LocationId }).IsUnique();
            e.HasOne(s => s.Product).WithMany(p => p.StockLevels).HasForeignKey(s => s.ProductId);
            e.HasOne(s => s.Location).WithMany().HasForeignKey(s => s.LocationId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.Property(o => o.Customer).HasMaxLength(200);
            e.Property(o => o.StatusCode).HasMaxLength(8);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasOne(l => l.Order).WithMany(o => o.Lines).HasForeignKey(l => l.OrderId);
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
            e.Ignore(l => l.QuantityOpen);
        });

        modelBuilder.Entity<Reservation>(e =>
        {
            e.HasOne(r => r.OrderLine).WithMany(l => l.Reservations).HasForeignKey(r => r.OrderLineId);
            e.HasOne(r => r.Location).WithMany().HasForeignKey(r => r.LocationId);
        });
    }
}
