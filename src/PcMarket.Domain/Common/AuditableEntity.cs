namespace PcMarket.Domain.Common;

/// <summary>An entity that tracks creation and last-modification timestamps (UTC).</summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
