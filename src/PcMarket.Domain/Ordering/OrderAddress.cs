namespace PcMarket.Domain.Ordering;

/// <summary>Immutable snapshot of the delivery address taken at order time, stored as JSONB so the
/// order stays correct even if the user later edits or deletes the source address.</summary>
public class OrderAddress
{
    public required string Region { get; init; }
    public required string City { get; init; }
    public required string Street { get; init; }
    public string? Details { get; init; }

    /// <summary>Where the customer actually is, when they pinned it on a map — the Telegram bot asks for this
    /// instead of a written region and city, because a pin plus a flat number gets a courier to the door and
    /// a typed address often does not. Null for orders placed from the web, which collects the address in
    /// writing. Stored as part of the same JSON document, so it costs no schema change.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>True when this order carries a map pin worth showing to whoever delivers it.</summary>
    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}
