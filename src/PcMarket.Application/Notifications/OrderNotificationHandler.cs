using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.Application.Notifications;

/// <summary>Turns order domain events into (a) an immediate real-time push and (b) a durable, retried
/// notification-delivery job. One class handles all three order events.</summary>
public sealed class OrderNotificationHandler(INotificationJobScheduler scheduler, IRealtimeNotifier realtime) :
    IDomainEventHandler<OrderPlacedEvent>,
    IDomainEventHandler<OrderPaidEvent>,
    IDomainEventHandler<OrderStatusChangedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent e, CancellationToken cancellationToken = default)
    {
        scheduler.EnqueueOrderNotification(e.OrderId, NotificationType.OrderCreated);
        await realtime.NewOrderAsync(e.OrderId, e.OrderNumber, cancellationToken);
    }

    public async Task HandleAsync(OrderPaidEvent e, CancellationToken cancellationToken = default)
    {
        scheduler.EnqueueOrderNotification(e.OrderId, NotificationType.OrderPaid);
        await realtime.OrderStatusChangedAsync(e.UserId, e.OrderNumber, OrderStatus.Paid.ToString(), cancellationToken);
    }

    public async Task HandleAsync(OrderStatusChangedEvent e, CancellationToken cancellationToken = default)
    {
        // The Paid transition is covered by OrderPaidEvent — avoid a duplicate durable notification.
        if (e.ToStatus != OrderStatus.Paid)
        {
            scheduler.EnqueueOrderNotification(e.OrderId, NotificationType.OrderStatusChanged);
        }

        await realtime.OrderStatusChangedAsync(e.UserId, e.OrderNumber, e.ToStatus.ToString(), cancellationToken);
    }
}
