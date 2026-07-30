using PcMarket.Domain.Common;

namespace PcMarket.Domain.Ordering;

/// <summary>A shopping cart. Authenticated carts key on <see cref="UserId"/>; guest carts key on
/// <see cref="Token"/> (also mirrored in Redis) and are merged into the user cart on login.</summary>
public class Cart : AuditableEntity
{
    public Guid? UserId { get; set; }
    public string? Token { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}
