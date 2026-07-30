using PcMarket.Domain.Enums;

namespace PcMarket.Application.Abstractions.Notifications;

/// <summary>Enqueues durable, retried background delivery of order notifications. Implemented over Hangfire
/// in the API host so the Application layer stays free of the job framework.</summary>
public interface INotificationJobScheduler
{
    void EnqueueOrderNotification(Guid orderId, NotificationType type);
}
