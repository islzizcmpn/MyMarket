using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Messaging;
using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;

namespace PcMarket.Infrastructure.Notifications;

/// <summary>Base for the dev/stub channels: logs the outbound message and reports success. Concrete channels
/// (Telegram/push/SMS/email) are swapped for live gateways in later phases without touching callers.</summary>
public abstract class LoggingNotificationChannel(ILogger logger, NotificationSettings settings) : INotificationChannel
{
    public abstract NotificationChannel Channel { get; }

    protected abstract bool ConfiguredEnabled(NotificationSettings s);

    public bool IsEnabled => ConfiguredEnabled(settings);

    public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[{Channel}] → user {UserId}: {Title} — {Body}", Channel, message.UserId, message.Title, message.Body);
        return Task.FromResult(true);
    }
}

/// <summary>Delivers over Telegram to the recipient's linked chat. Falls back to logging (and reports success)
/// when no bot token is configured or the customer has not linked their Telegram account — neither is a
/// delivery failure, so it must not burn the notification's retries.</summary>
public sealed class TelegramNotificationChannel(
    ILogger<TelegramNotificationChannel> logger,
    IOptions<NotificationSettings> settings,
    ITelegramMessenger messenger,
    ITelegramLinkStore links) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Telegram;

    public bool IsEnabled => settings.Value.Telegram;

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!messenger.IsConfigured || message.UserId is not { } userId)
        {
            logger.LogInformation("[Telegram] → user {UserId}: {Title} — {Body}", message.UserId, message.Title, message.Body);
            return true;
        }

        var chatId = await links.GetTelegramUserIdAsync(userId, cancellationToken);
        if (chatId is null)
        {
            logger.LogDebug("Telegram notification skipped: user {UserId} has no linked Telegram account.", userId);
            return true;
        }

        return await messenger.SendTextAsync(chatId.Value, $"{message.Title}\n\n{message.Body}", cancellationToken);
    }
}

/// <summary>Delivers over push to every device the recipient has registered. Like the Telegram channel, a
/// recipient with no registered devices — or a host with no push credentials — logs and reports success, since
/// neither is a delivery failure and neither should burn the notification's retries.</summary>
public sealed class PushNotificationChannel(
    ILogger<PushNotificationChannel> logger,
    IOptions<NotificationSettings> settings,
    IPushSender sender,
    IApplicationDbContext db) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Push;

    public bool IsEnabled => settings.Value.Push;

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.UserId is not { } userId)
        {
            logger.LogInformation("[Push] → user {UserId}: {Title} — {Body}", message.UserId, message.Title, message.Body);
            return true;
        }

        var tokens = await db.DeviceTokens
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            logger.LogDebug("Push notification skipped: user {UserId} has no registered devices.", userId);
            return true;
        }

        // One failing device must not hide a successful delivery to the user's other devices.
        var delivered = false;
        foreach (var token in tokens)
        {
            delivered |= await sender.SendAsync(
                new PushMessage(token, message.Title, message.Body, message.Data), cancellationToken);
        }

        return delivered;
    }
}

public sealed class SmsNotificationChannel(ILogger<SmsNotificationChannel> logger, IOptions<NotificationSettings> settings)
    : LoggingNotificationChannel(logger, settings.Value)
{
    public override NotificationChannel Channel => NotificationChannel.Sms;
    protected override bool ConfiguredEnabled(NotificationSettings s) => s.Sms;
}

public sealed class EmailNotificationChannel(ILogger<EmailNotificationChannel> logger, IOptions<NotificationSettings> settings)
    : LoggingNotificationChannel(logger, settings.Value)
{
    public override NotificationChannel Channel => NotificationChannel.Email;
    protected override bool ConfiguredEnabled(NotificationSettings s) => s.Email;
}
