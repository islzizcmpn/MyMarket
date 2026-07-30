using PcMarket.Domain.Common;

namespace PcMarket.Domain.Catalog;

/// <summary>An image reference for a product (optionally scoped to a single variant).</summary>
public class ProductImage : Entity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public required string Url { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Product Product { get; set; } = null!;
}
