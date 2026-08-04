using PcMarket.Contracts.Catalog;
using PcMarket.Domain.Catalog;

namespace PcMarket.Application.Catalog;

/// <summary>Hand-written projections from catalog entities to their DTOs.</summary>
public static class CatalogMappings
{
    public static BrandDto ToDto(this Brand brand) =>
        new(brand.Id, brand.Name, brand.Slug, brand.LogoUrl);

    public static ProductImageDto ToDto(this ProductImage image) =>
        new(image.Url, image.IsPrimary, image.SortOrder);

    public static ProductVariantDto ToDto(this ProductVariant variant) =>
        new(variant.Id, variant.Sku, variant.Attributes, variant.Price, variant.OldPrice, variant.StockQty);

    public static ProductDetailDto ToDetailDto(this Product product) =>
        new(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.CategoryId,
            product.Brand?.Name,
            product.Specs,
            product.Images.OrderBy(i => i.SortOrder).Select(i => i.ToDto()).ToList(),
            product.Variants.Where(v => v.IsActive).OrderBy(v => v.Price).Select(v => v.ToDto()).ToList());

    public static ProductListItemDto ToListItemDto(this Product product)
    {
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary)
                           ?? product.Images.OrderBy(i => i.SortOrder).FirstOrDefault();

        // Primary first, then the rest by sort order — the card's thumbnail dots render in this order.
        var imageUrls = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(i => i.Url)
            .ToList();

        return new ProductListItemDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Brand?.Name,
            primaryImage?.Url,
            activeVariants.Count > 0 ? activeVariants.Min(v => v.Price) : 0m,
            activeVariants.Any(v => v.StockQty > 0),
            imageUrls);
    }
}
