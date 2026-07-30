using PcMarket.Domain.Common;

namespace PcMarket.Domain.Ordering;

/// <summary>A line in an <see cref="Order"/>. Name and price are snapshotted at order time.</summary>
public class OrderItem : Entity
{
    public Guid OrderId { get; set; }
    public Guid ProductVariantId { get; set; }
    public required string NameSnapshot { get; set; }
    public decimal UnitPrice { get; set; }
    public int Qty { get; set; }

    public decimal LineTotal => UnitPrice * Qty;

    public Order Order { get; set; } = null!;
}
