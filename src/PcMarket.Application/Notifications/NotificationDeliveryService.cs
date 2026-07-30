using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Notifications;
using PcMarket.Domain.Ordering;

namespace PcMarket.Application.Notifications;

/// <summary>Fans an order notification out across every enabled channel, recording a <see cref="Notification"/>
/// ledger row per channel with its delivery outcome. Invoked from a Hangfire job, so throwing here triggers
/// Hangfire's automatic retry; per-channel sends are also retried a few times for transient failures.</summary>
public sealed class NotificationDeliveryService(
    IApplicationDbContext db,
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationDeliveryService> logger)
{
    private const int MaxSendAttempts = 3;

    public async Task DeliverOrderNotificationAsync(Guid orderId, NotificationType type, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Notification skipped: order {OrderId} not found.", orderId);
            return;
        }

        var message = Compose(order, type);
        var enabled = channels.Where(c => c.IsEnabled).ToList();

        foreach (var channel in enabled)
        {
            var notification = new Notification
            {
                UserId = order.UserId,
                Channel = channel.Channel,
                Type = type,
                Payload = new Dictionary<string, string>(message.Data)
                {
                    ["title"] = message.Title,
                    ["body"] = message.Body
                }
            };
            db.Notifications.Add(notification);

            var sent = await TrySendAsync(channel, message, cancellationToken);
            notification.Status = sent ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SentAt = sent ? DateTimeOffset.UtcNow : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TrySendAsync(INotificationChannel channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            try
            {
                if (await channel.SendAsync(message, cancellationToken))
                {
                    return true;
                }
            }
            catch (Exception ex) when (attempt < MaxSendAttempts)
            {
                logger.LogWarning(ex, "Channel {Channel} send attempt {Attempt} failed; retrying.", channel.Channel, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
        }

        return false;
    }

    private static NotificationMessage Compose(Order order, NotificationType type)
    {
        var (title, body) = type switch
        {
            NotificationType.OrderCreated => ("Order placed", $"Your order {order.Number} has been placed."),
            NotificationType.OrderPaid => ("Payment received", $"Payment for order {order.Number} was received. Thank you!"),
            NotificationType.OrderStatusChanged => ("Order updated", $"Order {order.Number} is now {order.Status}."),
            NotificationType.PaymentFailed => ("Payment failed", $"Payment for order {order.Number} could not be completed."),
            _ => ("Order notification", $"Update for order {order.Number}.")
        };

        var data = new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(),
            ["orderNumber"] = order.Number,
            ["status"] = order.Status.ToString()
        };

        return new NotificationMessage(order.UserId, type, title, body, data);
    }
}
