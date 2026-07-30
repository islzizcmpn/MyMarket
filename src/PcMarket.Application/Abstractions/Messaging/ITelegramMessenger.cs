namespace PcMarket.Application.Abstractions.Messaging;

/// <summary>Sends plain-text messages out over Telegram. The live implementation lives in
/// <c>PcMarket.Bot</c> (it owns the bot client); a no-op stands in when no bot token is configured, so
/// callers — notably the Telegram notification channel — never have to know which is wired up.</summary>
public interface ITelegramMessenger
{
    /// <summary>Whether a usable bot token is configured. False means every send is a no-op.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends a plain-text message to a chat; returns whether the send succeeded.</summary>
    Task<bool> SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default);
}
