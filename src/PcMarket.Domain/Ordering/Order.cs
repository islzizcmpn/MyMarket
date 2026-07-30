using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.Domain.Ordering;

/// <summary>The order aggregate root. Owns its items, status history, and enforces the
/// legal status transitions of the order lifecycle.</summary>
public class Order : AuditableEntity
{
    /// <summary>Legal next states for each status. Terminal states map to an empty set.</summary>
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Created] = [OrderStatus.AwaitingPayment, OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.AwaitingPayment] = [OrderStatus.Paid, OrderStatus.Cancelled],
            [OrderStatus.Paid] = [OrderStatus.Processing, OrderStatus.Refunded, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Refunded],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Refunded],
            [OrderStatus.Delivered] = [OrderStatus.Refunded],
            [OrderStatus.Cancelled] = [],
            [OrderStatus.Refunded] = []
        };

    public required string Number { get; set; }
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.None;
    public DeliveryType DeliveryType { get; set; }

    /// <summary>Snapshot of the delivery address, persisted as JSONB.</summary>
    public required OrderAddress ShippingAddress { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = MoneyConstants.DefaultCurrency;

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];

    /// <summary>The legal next states from <paramref name="status"/>. Exposed so callers that render
    /// transition choices (admin panel, Telegram admin channel) read them off the state machine itself
    /// rather than duplicating the table.</summary>
    public static IReadOnlyList<OrderStatus> AllowedFrom(OrderStatus status) =>
        AllowedTransitions.TryGetValue(status, out var allowed) ? allowed : [];

    /// <summary>Returns whether a transition to <paramref name="next"/> is currently legal.</summary>
    public bool CanTransitionTo(OrderStatus next) =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(next);

    /// <summary>Applies a status transition, recording history and raising the matching domain events.</summary>
    /// <exception cref="DomainException">If the transition is not legal from the current status.</exception>
    public void TransitionTo(OrderStatus next, string? changedBy = null)
    {
        if (next == Status)
        {
            return;
        }

        if (!CanTransitionTo(next))
        {
            throw new DomainException($"Illegal order transition {Status} -> {next} for order {Number}.");
        }

        var from = Status;
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = Id,
            FromStatus = from,
            ToStatus = next,
            ChangedBy = changedBy
        });

        Raise(new OrderStatusChangedEvent(Id, Number, UserId, from, next));

        if (next == OrderStatus.Paid)
        {
            PaymentStatus = PaymentStatus.Paid;
            Raise(new OrderPaidEvent(Id, Number, UserId));
        }
    }
}
