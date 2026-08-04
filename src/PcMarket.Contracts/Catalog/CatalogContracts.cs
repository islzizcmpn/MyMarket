namespace PcMarket.Contracts.Catalog;

/// <summary>A category and its subtree.</summary>
public sealed record CategoryNodeDto(
    Guid Id,
    string Name,
    string Slug,
    int SortOrder,
    IReadOnlyList<CategoryNodeDto> Children);

public sealed record BrandDto(Guid Id, string Name, string Slug, string? LogoUrl);

public sealed record ProductImageDto(string Url, bool IsPrimary, int SortOrder);

public sealed record ProductVariantDto(
    Guid Id,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    decimal? OldPrice,
    int StockQty);

/// <summary>Compact product shape for listing/search grids.</summary>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string? BrandName,
    string? PrimaryImageUrl,
    decimal PriceFrom,
    bool InStock,
    // Every image, primary first, so grid cards can offer the thumbnail selector without a detail
    // round-trip. Defaulted so existing constructor call sites and older clients keep working.
    IReadOnlyList<string>? ImageUrls = null);

/// <summary>Full product detail for the product page.</summary>
public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid CategoryId,
    string? BrandName,
    IReadOnlyDictionary<string, string> Specs,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants);

/// <summary>Sort options for product listings.</summary>
public enum ProductSort
{
    Newest = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    NameAsc = 3
}
