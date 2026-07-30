using PcMarket.Domain.Catalog;
using PcMarket.Domain.Common;

namespace PcMarket.Domain.Ordering;

/// <summary>A line in a <see cref="Cart"/>, capturing the price seen when it was added.</summary>
public class CartItem : Entity
{
    public Guid CartId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPriceSnapshot { get; set; }

    public Cart Cart { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}
