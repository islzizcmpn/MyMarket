using PcMarket.Domain.Ordering;
using DomainEnums = PcMarket.Domain.Enums;
using Dto = PcMarket.Contracts.Orders;

namespace PcMarket.Application.Orders;

/// <summary>Maps order aggregates to their wire DTOs. The contract enums mirror the domain enums by value,
/// so the cross-cast is exact.</summary>
internal static class OrderMappings
{
    public static Dto.OrderStatus ToDto(this DomainEnums.OrderStatus s) => (Dto.OrderStatus)(int)s;
    public static Dto.PaymentStatus ToDto(this DomainEnums.PaymentStatus s) => (Dto.PaymentStatus)(int)s;
    public static Dto.PaymentMethod ToDto(this DomainEnums.PaymentMethod s) => (Dto.PaymentMethod)(int)s;
    public static Dto.DeliveryType ToDto(this DomainEnums.DeliveryType s) => (Dto.DeliveryType)(int)s;

    public static DomainEnums.PaymentMethod ToDomain(this Dto.PaymentMethod s) => (DomainEnums.PaymentMethod)(int)s;
    public static DomainEnums.DeliveryType ToDomain(this Dto.DeliveryType s) => (DomainEnums.DeliveryType)(int)s;

    public static Dto.OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Number,
        order.Status.ToDto(),
        order.PaymentMethod.ToDto(),
        order.PaymentStatus.ToDto(),
        order.DeliveryType.ToDto(),
        new Dto.ShippingAddressDto(
            order.ShippingAddress.Region,
            order.ShippingAddress.City,
            order.ShippingAddress.Street,
            order.ShippingAddress.Details),
        order.Subtotal,
        order.DeliveryFee,
        order.Total,
        order.Currency,
        order.CreatedAt,
        order.Items
            .Select(i => new Dto.OrderItemDto(i.Id, i.ProductVariantId, i.NameSnapshot, i.UnitPrice, i.Qty, i.LineTotal))
            .ToList(),
        order.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new Dto.OrderStatusHistoryDto(h.FromStatus?.ToDto(), h.ToStatus.ToDto(), h.ChangedBy, h.ChangedAt))
            .ToList());

    public static Dto.OrderListItemDto ToListItem(this Order order) => new(
        order.Id,
        order.Number,
        order.Status.ToDto(),
        order.PaymentStatus.ToDto(),
        order.Total,
        order.Items.Sum(i => i.Qty),
        order.CreatedAt);
}
