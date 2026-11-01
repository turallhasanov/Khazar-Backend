using Khazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Khazar.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(16);
        builder.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(320);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

        // Stored as text so the column stays readable and enum reordering cannot corrupt data.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);

        // Address is a value object without identity: its components become columns of the Orders table.
        builder.ComplexProperty(o => o.ShippingAddress, address =>
        {
            address.IsRequired();
            address.Property(a => a.Street).HasColumnName("ShippingStreet").IsRequired().HasMaxLength(300);
            address.Property(a => a.City).HasColumnName("ShippingCity").IsRequired().HasMaxLength(100);
            address.Property(a => a.ZipCode).HasColumnName("ShippingZipCode").IsRequired().HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("ShippingCountry").IsRequired().HasMaxLength(100);
        });

        // Backs the collision retry contract of IOrderRepository.CreateAsync.
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CustomerEmail);

        // Guest orders have no user; deleting an account keeps its order history.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(o => o.UserId);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
