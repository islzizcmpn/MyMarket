using Microsoft.AspNetCore.SignalR;
using PcMarket.Application.Abstractions.Notifications;

namespace PcMarket.Api.Realtime;

/// <summary>Pushes order events to connected clients over SignalR — status changes to the owning customer's
/// group, new-order alerts to the admin feed.</summary>
public sealed class SignalRRealtimeNotifier(
    IHubContext<OrderStatusHub> orderHub,
    IHubContext<AdminOrderHub> adminHub) : IRealtimeNotifier
{
    public Task OrderStatusChangedAsync(Guid userId, string orderNumber, string status, CancellationToken cancellationToken = default) =>
        orderHub.Clients.Group(OrderStatusHub.UserGroup(userId))
            .SendAsync("OrderStatusChanged", new { orderNumber, status }, cancellationToken);

    public Task NewOrderAsync(Guid orderId, string orderNumber, CancellationToken cancellationToken = default) =>
        adminHub.Clients.Group(AdminOrderHub.AdminGroup)
            .SendAsync("NewOrder", new { orderId, orderNumber }, cancellationToken);
}
