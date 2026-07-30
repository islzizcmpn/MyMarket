using PcMarket.Domain.Enums;

namespace PcMarket.Application.Abstractions.Notifications;

/// <summary>A rendered outbound message, channel-agnostic.</summary>
/// <param name="UserId">Recipient user, or null for broadcast/admin messages.</param>
/// <param name="Type">The business event that triggered it.</param>
/// <param name="Title">Short heading.</param>
/// <param name="Body">Message body.</param>
/// <param name="Data">Extra key/value payload (deep links, order number, …).</param>
public sealed record NotificationMessage(
    Guid? UserId,
    NotificationType Type,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

/// <summary>One outbound delivery channel (Telegram, push, SMS, email). Implementations are swappable and,
/// until live credentials exist, are dev stubs that log.</summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    /// <summary>Whether the channel is switched on via configuration.</summary>
    bool IsEnabled { get; }

    /// <summary>Delivers the message; returns whether delivery succeeded.</summary>
    Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
