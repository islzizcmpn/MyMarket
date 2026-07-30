using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;

namespace PcMarket.Domain.Notifications;

/// <summary>An outbound message queued for delivery over a channel. The recipient user is referenced
/// by id only (the user lives in the identity store).</summary>
public class Notification : Entity
{
    public Guid? UserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationType Type { get; set; }

    /// <summary>Channel-specific payload (title, body, deep links) stored as JSONB.</summary>
    public Dictionary<string, string> Payload { get; set; } = [];

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}
