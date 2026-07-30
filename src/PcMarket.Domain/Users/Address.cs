using PcMarket.Domain.Common;

namespace PcMarket.Domain.Users;

/// <summary>A saved delivery address belonging to a user. The user lives in the identity store,
/// so this entity references it only by <see cref="UserId"/> (no navigation) to keep the domain pure.</summary>
public class Address : AuditableEntity
{
    public Guid UserId { get; set; }
    public required string Region { get; set; }
    public required string City { get; set; }
    public required string Street { get; set; }
    public string? Details { get; set; }
    public bool IsDefault { get; set; }
}
