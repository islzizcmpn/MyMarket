using PcMarket.Domain.Common;

namespace PcMarket.Domain.Admin;

/// <summary>An immutable record of a back-office action (who did what, to which entity, when).</summary>
public class AuditLogEntry : Entity
{
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }

    /// <summary>Dotted action key, e.g. <c>product.update</c>, <c>order.status-advance</c>.</summary>
    public required string Action { get; set; }

    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Summary { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
