namespace PcMarket.Application.Abstractions.Notifications;

/// <summary>Pushes live updates over the real-time transport (SignalR): order-status changes to the owning
/// customer and new-order alerts to the admin feed. Implemented in the API host.</summary>
public interface IRealtimeNotifier
{
    Task OrderStatusChangedAsync(Guid userId, string orderNumber, string status, CancellationToken cancellationToken = default);

    Task NewOrderAsync(Guid orderId, string orderNumber, CancellationToken cancellationToken = default);
}
