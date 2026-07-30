namespace PcMarket.Infrastructure.Persistence.Seed;

/// <summary>Root document for a catalog import (see <c>demo-catalog.json</c>).</summary>
public sealed class CatalogSeed
{
    public List<BrandSeed> Brands { get; set; } = [];
    public List<CategorySeed> Categories { get; set; } = [];
    public List<ProductSeed> Products { get; set; } = [];
}

public sealed class BrandSeed
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? LogoUrl { get; set; }
}

public sealed class CategorySeed
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ParentSlug { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProductSeed
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
    public string? BrandSlug { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> Specs { get; set; } = [];
    public List<VariantSeed> Variants { get; set; } = [];
    public List<string> Images { get; set; } = [];
}

public sealed class VariantSeed
{
    public string Sku { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = [];
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public int StockQty { get; set; }
}

/// <summary>Counts of newly inserted rows from an import (existing rows are left untouched).</summary>
public readonly record struct CatalogImportResult(int Brands, int Categories, int Products);
