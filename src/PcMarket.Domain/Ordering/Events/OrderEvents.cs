using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;

namespace PcMarket.Domain.Ordering.Events;

/// <summary>Raised when a new order is placed.</summary>
public sealed record OrderPlacedEvent(Guid OrderId, string OrderNumber, Guid UserId) : IDomainEvent;

/// <summary>Raised on every accepted order status transition.</summary>
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    OrderStatus FromStatus,
    OrderStatus ToStatus) : IDomainEvent;

/// <summary>Raised when an order becomes fully paid.</summary>
public sealed record OrderPaidEvent(Guid OrderId, string OrderNumber, Guid UserId) : IDomainEvent;
