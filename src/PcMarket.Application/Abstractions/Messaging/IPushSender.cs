namespace PcMarket.Application.Abstractions.Messaging;

/// <summary>A push message addressed to one device registration token.</summary>
/// <param name="Token">The provider registration token (FCM).</param>
/// <param name="Title">Notification heading.</param>
/// <param name="Body">Notification body.</param>
/// <param name="Data">Extra key/value payload carried to the app (order id, deep link, …).</param>
public sealed record PushMessage(
    string Token,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

/// <summary>Delivers push notifications to mobile devices. Mirrors <see cref="ITelegramMessenger"/>: a
/// logging stand-in is registered by default so the notification pipeline works without a Firebase project,
/// and a live FCM sender replaces it once credentials exist — callers never change.</summary>
public interface IPushSender
{
    /// <summary>Whether real credentials are configured. False means every send is logged, not delivered.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends one message; returns whether delivery succeeded.</summary>
    Task<bool> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}
