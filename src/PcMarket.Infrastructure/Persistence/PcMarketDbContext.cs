using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Admin;
using PcMarket.Domain.Catalog;
using PcMarket.Domain.Common;
using PcMarket.Domain.Content;
using PcMarket.Domain.Notifications;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Domain.Users;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Persistence;

/// <summary>The single application DbContext. Extends the Identity context (users/roles) and adds the
/// domain aggregates. Entity mapping lives in <c>Persistence/Configurations</c>. After a successful save it
/// dispatches any buffered domain events (best-effort; the dispatcher is null at design time / in migrations).</summary>
public class PcMarketDbContext(DbContextOptions<PcMarketDbContext> options, IDomainEventDispatcher? dispatcher = null)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<CmsBlock> CmsBlocks => Set<CmsBlock>();
    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PcMarketDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            var entity = builder.Entity(entityType.ClrType);
            // Domain-event buffers are behavior, never persisted.
            entity.Ignore(nameof(Entity.DomainEvents));
            // Ids are assigned in the entity constructor (Guid v7); EF must not treat them as
            // store-generated, otherwise insert-then-update in one unit of work misfires.
            entity.Property(nameof(Entity.Id)).ValueGeneratedNever();
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>()
            .HavePrecision(MoneyConstants.Precision, MoneyConstants.Scale);
    }

    /// <summary>Persists changes, then dispatches domain events raised by the saved aggregates. Events are
    /// collected and cleared only after a successful commit so a failed save raises nothing.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        if (dispatcher is null)
        {
            return result;
        }

        var entities = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entities.Count == 0)
        {
            return result;
        }

        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        await dispatcher.DispatchAsync(domainEvents, cancellationToken);
        return result;
    }
}
