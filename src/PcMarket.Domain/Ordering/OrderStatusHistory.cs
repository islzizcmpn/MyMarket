using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;

namespace PcMarket.Domain.Ordering;

/// <summary>Audit record of a single order status transition.</summary>
public class OrderStatusHistory : Entity
{
    public Guid OrderId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
