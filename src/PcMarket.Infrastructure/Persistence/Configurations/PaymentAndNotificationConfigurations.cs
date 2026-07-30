using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcMarket.Domain.Notifications;
using PcMarket.Domain.Payments;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");
        b.Property(x => x.ProviderTxnId).HasMaxLength(200);
        b.Property(x => x.RawPayload).HasColumnType("jsonb");

        // Idempotency: a given gateway transaction id maps to at most one ledger row.
        b.HasIndex(x => new { x.Provider, x.ProviderTxnId })
            .IsUnique()
            .HasFilter("\"ProviderTxnId\" IS NOT NULL");

        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");

        b.Property(x => x.Payload)
            .HasConversion(JsonbConversions.Dictionary, JsonbConversions.DictionaryComparer)
            .HasColumnType("jsonb");

        b.HasIndex(x => new { x.Status, x.Channel });

        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("DeviceTokens");
        b.Property(x => x.Token).HasMaxLength(400).IsRequired();

        // One row per registration token, so a re-register updates rather than duplicates.
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);

        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
