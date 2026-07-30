using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcMarket.Domain.Ordering;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("Carts");
        b.Property(x => x.Token).HasMaxLength(100);
        b.HasIndex(x => x.Token).IsUnique().HasFilter("\"Token\" IS NOT NULL");
        b.HasIndex(x => x.UserId);

        b.HasMany(x => x.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("CartItems");

        b.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.Property(x => x.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);

        // Delivery address snapshot persisted as a JSONB document column.
        b.OwnsOne(x => x.ShippingAddress, nb => nb.ToJson());

        b.HasMany(x => x.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.StatusHistory)
            .WithOne(h => h.Order)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems");
        b.Property(x => x.NameSnapshot).HasMaxLength(300).IsRequired();
        b.Ignore(x => x.LineTotal);
        b.HasIndex(x => x.ProductVariantId);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("OrderStatusHistory");
        b.Property(x => x.ChangedBy).HasMaxLength(200);
    }
}
