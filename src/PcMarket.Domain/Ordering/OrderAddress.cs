namespace PcMarket.Domain.Ordering;

/// <summary>Immutable snapshot of the delivery address taken at order time, stored as JSONB so the
/// order stays correct even if the user later edits or deletes the source address.</summary>
public class OrderAddress
{
    public required string Region { get; init; }
    public required string City { get; init; }
    public required string Street { get; init; }
    public string? Details { get; init; }
}
