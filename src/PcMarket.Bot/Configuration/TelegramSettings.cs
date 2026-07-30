namespace PcMarket.Bot.Configuration;

/// <summary>Telegram bot configuration, bound from the <c>Telegram</c> section. The bot is off by default:
/// without a token and a webhook secret it cannot be driven, and every outbound send degrades to a no-op.</summary>
public sealed class TelegramSettings
{
    /// <summary>Path the webhook endpoint is mapped on. Telegram must be pointed at
    /// <c>{PublicApiUrl}{WebhookPath}</c>.</summary>
    public const string WebhookPath = "/api/v1/bot/telegram/webhook";

    public bool Enabled { get; set; }

    /// <summary>BotFather token. Blank disables all outbound Telegram traffic.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Shared secret Telegram echoes back in <c>X-Telegram-Bot-Api-Secret-Token</c>. Required —
    /// the webhook rejects every update when it is unset.</summary>
    public string WebhookSecretToken { get; set; } = string.Empty;

    /// <summary>Chat that receives new-order alerts (a manager's private chat, a group, or a channel).</summary>
    public long? AdminChatId { get; set; }

    /// <summary>Public base URL of the storefront, used for "open in store" deep links.</summary>
    public string StorefrontUrl { get; set; } = string.Empty;

    /// <summary>Public HTTPS base URL of this API, used when registering the webhook with Telegram.</summary>
    public string PublicApiUrl { get; set; } = string.Empty;

    /// <summary>Products shown per catalog page in the bot.</summary>
    public int PageSize { get; set; } = 6;

    /// <summary>Whether customers may link by <em>typing</em> a phone number. Off by default, because that is
    /// the only route in the bot that costs an SMS: a typed number is unverified, so it has to be confirmed with
    /// an OTP. Sharing the contact card stays available either way and needs no SMS at all.
    ///
    /// Leaving this off is not a security compromise — the typed route is the weaker of the two, and disabling
    /// it removes the OTP brute-force surface and the SMS-pumping cost. It does mean a customer whose PcMarket
    /// account is registered to a <em>different</em> number than their Telegram account cannot link that account
    /// from the bot. Turn this on once an SMS provider is funded.</summary>
    public bool AllowPhoneEntry { get; set; }
}
