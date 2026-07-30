using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using PcMarket.Domain.Catalog;

namespace PcMarket.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        b.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("Brands");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.Property(x => x.LogoUrl).HasMaxLength(1000);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(320).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        b.Property(x => x.Specs)
            .HasConversion(JsonbConversions.Dictionary, JsonbConversions.DictionaryComparer)
            .HasColumnType("jsonb");
        b.HasIndex(x => x.Specs).HasMethod("gin");

        b.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Brand)
            .WithMany(br => br.Products)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // Stored, generated full-text search column + GIN index (queried in the Catalog module).
        b.Property<NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql(
                "to_tsvector('simple', coalesce(\"Name\", '') || ' ' || coalesce(\"Description\", ''))",
                stored: true);
        b.HasIndex("SearchVector").HasMethod("gin");
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.ToTable("ProductVariants");
        b.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Sku).IsUnique();

        b.Property(x => x.Attributes)
            .HasConversion(JsonbConversions.Dictionary, JsonbConversions.DictionaryComparer)
            .HasColumnType("jsonb");

        b.HasOne(x => x.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.ToTable("ProductImages");
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => x.VariantId);

        b.HasOne(x => x.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
