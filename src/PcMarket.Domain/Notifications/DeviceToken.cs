using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;

namespace PcMarket.Domain.Notifications;

/// <summary>A push registration token for one install of the mobile app. A user may hold several (one per
/// device); a token is globally unique, so re-registering the same token moves it to the current user rather
/// than creating a duplicate — reinstalls and device hand-offs reuse tokens.</summary>
public class DeviceToken : Entity
{
    public Guid UserId { get; set; }

    /// <summary>The provider registration token (FCM). Opaque to us.</summary>
    public string Token { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Refreshed every time the app re-registers, so stale installs can be pruned later.</summary>
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
