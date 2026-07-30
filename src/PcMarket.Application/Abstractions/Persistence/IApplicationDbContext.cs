using Microsoft.EntityFrameworkCore;
using PcMarket.Domain.Admin;
using PcMarket.Domain.Catalog;
using PcMarket.Domain.Content;
using PcMarket.Domain.Notifications;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Domain.Users;

namespace PcMarket.Application.Abstractions.Persistence;

/// <summary>Application-facing view of the database. The concrete EF Core context implements this so
/// use-case services depend on an abstraction, not on Infrastructure directly.</summary>
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistory { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<AuditLogEntry> AuditLog { get; }
    DbSet<Banner> Banners { get; }
    DbSet<CmsBlock> CmsBlocks { get; }
    DbSet<ContentTranslation> ContentTranslations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
