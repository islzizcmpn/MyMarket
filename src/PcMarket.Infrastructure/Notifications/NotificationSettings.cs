namespace PcMarket.Infrastructure.Notifications;

/// <summary>Feature flags for outbound notification channels. All default on; the channels are dev stubs
/// until live Telegram/FCM/SMS/email credentials are wired in later phases.</summary>
public sealed class NotificationSettings
{
    public bool Telegram { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool Sms { get; set; } = true;
    public bool Email { get; set; } = true;
}
