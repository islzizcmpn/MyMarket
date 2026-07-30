namespace PcMarket.Contracts.Orders;

/// <summary>Snapshot of the delivery address carried on an order.</summary>
public sealed record ShippingAddressDto(
    string Region,
    string City,
    string Street,
    string? Details);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductVariantId,
    string Name,
    decimal UnitPrice,
    int Qty,
    decimal LineTotal);

public sealed record OrderStatusHistoryDto(
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    string? ChangedBy,
    DateTimeOffset ChangedAt);

public sealed record OrderDto(
    Guid Id,
    string Number,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DeliveryType DeliveryType,
    ShippingAddressDto ShippingAddress,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    string Currency,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> History);

public sealed record OrderListItemDto(
    Guid Id,
    string Number,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    decimal Total,
    int ItemCount,
    DateTimeOffset CreatedAt);

/// <summary>Checkout request. Supply either a saved <see cref="AddressId"/> or an inline
/// <see cref="Address"/>; for courier delivery an address is required.</summary>
public sealed record CreateOrderRequest(
    PaymentMethod PaymentMethod,
    DeliveryType DeliveryType,
    Guid? AddressId,
    ShippingAddressDto? Address);
