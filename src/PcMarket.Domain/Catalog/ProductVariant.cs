using PcMarket.Domain.Common;

namespace PcMarket.Domain.Catalog;

/// <summary>A concrete, purchasable SKU of a <see cref="Product"/> with its own price and stock.</summary>
public class ProductVariant : AuditableEntity
{
    public Guid ProductId { get; set; }
    public required string Sku { get; set; }

    /// <summary>Distinguishing attributes (e.g. color, capacity) stored as JSONB.</summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public int StockQty { get; set; }
    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;
}
