using Hangfire;
using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Application.Notifications;
using PcMarket.Domain.Enums;

namespace PcMarket.Api.Notifications;

/// <summary>Enqueues durable notification delivery onto Hangfire; the job resolves
/// <see cref="NotificationDeliveryService"/> in its own scope and Hangfire retries it on failure.</summary>
public sealed class HangfireNotificationJobScheduler(IBackgroundJobClient jobs) : INotificationJobScheduler
{
    public void EnqueueOrderNotification(Guid orderId, NotificationType type) =>
        jobs.Enqueue<NotificationDeliveryService>(s => s.DeliverOrderNotificationAsync(orderId, type, CancellationToken.None));
}
