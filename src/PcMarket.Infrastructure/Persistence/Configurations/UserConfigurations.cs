using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcMarket.Domain.Users;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Language).HasMaxLength(8);
        b.HasIndex(x => x.TelegramUserId).IsUnique().HasFilter("\"TelegramUserId\" IS NOT NULL");
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.ToTable("Addresses");
        b.Property(x => x.Region).HasMaxLength(120).IsRequired();
        b.Property(x => x.City).HasMaxLength(120).IsRequired();
        b.Property(x => x.Street).HasMaxLength(300).IsRequired();
        b.Property(x => x.Details).HasMaxLength(500);
        b.HasIndex(x => x.UserId);

        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
