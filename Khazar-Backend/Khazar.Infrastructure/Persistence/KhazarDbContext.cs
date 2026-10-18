using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence;

public sealed class KhazarDbContext(DbContextOptions<KhazarDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KhazarDbContext).Assembly);
    }
}
