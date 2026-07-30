using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcMarket.Domain.Admin;
using PcMarket.Domain.Content;

namespace PcMarket.Infrastructure.Persistence.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("AuditLog");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.ActorName).HasMaxLength(200);
        b.Property(x => x.Summary).HasMaxLength(500);
        b.HasIndex(x => x.CreatedAt);
    }
}

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> b)
    {
        b.ToTable("Banners");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Subtitle).HasMaxLength(400);
        b.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.LinkUrl).HasMaxLength(1000);
    }
}

public class CmsBlockConfiguration : IEntityTypeConfiguration<CmsBlock>
{
    public void Configure(EntityTypeBuilder<CmsBlock> b)
    {
        b.ToTable("CmsBlocks");
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
    }
}

public class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> b)
    {
        b.ToTable("ContentTranslations");
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Field).HasMaxLength(100).IsRequired();
        b.Property(x => x.Culture).HasMaxLength(10).IsRequired();
        b.Property(x => x.Value).IsRequired();

        // One value per field per language; also the lookup path for a batch of entities.
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.Field, x.Culture }).IsUnique();
    }
}
