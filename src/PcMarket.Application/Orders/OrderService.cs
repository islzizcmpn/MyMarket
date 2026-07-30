using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Ordering.Events;
using Dto = PcMarket.Contracts.Orders;

namespace PcMarket.Application.Orders;

/// <summary>Order use-cases: creation from the authenticated user's cart (stock check, price/name/address
/// snapshotting), listing, detail, and cancellation. Payment-method-specific state transitions are
/// delegated to the resolved <see cref="IPaymentProvider"/>.</summary>
public sealed class OrderService(IApplicationDbContext db, IPaymentProviderResolver providers)
{
    /// <summary>Creates an order from the user's cart, snapshotting item names/prices and the delivery
    /// address, reserving stock, and advancing the order via its payment provider (COD → Processing,
    /// online → AwaitingPayment).</summary>
    public async Task<Dto.OrderDto> CreateAsync(Guid userId, Dto.CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var method = request.PaymentMethod.ToDomain();
        var provider = providers.Resolve(method);

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new DomainException("Cart is empty.");

        if (cart.Items.Count == 0)
        {
            throw new DomainException("Cart is empty.");
        }

        var address = await ResolveAddressAsync(userId, request, cancellationToken);

        var variantIds = cart.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Include(v => v.Product)
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var items = new List<OrderItem>();
        foreach (var cartItem in cart.Items)
        {
            if (!variants.TryGetValue(cartItem.ProductVariantId, out var variant) || !variant.IsActive)
            {
                throw new DomainException("A product in your cart is no longer available.");
            }

            if (cartItem.Qty > variant.StockQty)
            {
                throw new DomainException($"Only {variant.StockQty} item(s) of '{variant.Product.Name}' in stock.");
            }

            variant.StockQty -= cartItem.Qty;
            items.Add(new OrderItem
            {
                ProductVariantId = variant.Id,
                NameSnapshot = variant.Product.Name,
                UnitPrice = variant.Price,
                Qty = cartItem.Qty
            });
        }

        var subtotal = items.Sum(i => i.LineTotal);
        const decimal deliveryFee = 0m;

        var order = new Order
        {
            Number = OrderNumberGenerator.Generate(),
            UserId = userId,
            PaymentMethod = method,
            DeliveryType = request.DeliveryType.ToDomain(),
            ShippingAddress = address,
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = subtotal + deliveryFee,
            Items = items
        };
        order.Raise(new OrderPlacedEvent(order.Id, order.Number, userId));

        db.Orders.Add(order);
        db.Carts.Remove(cart);

        // Advance the order into its post-checkout state for the chosen rail.
        await provider.InitiateAsync(order, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }

    public async Task<IReadOnlyList<Dto.OrderListItemDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);

        return orders.Select(o => o.ToListItem()).ToList();
    }

    public async Task<Dto.OrderDto?> GetAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(orderId, cancellationToken);
        return order is null || order.UserId != userId ? null : order.ToDto();
    }

    /// <summary>Cancels an order the caller owns, restoring reserved stock. Illegal from terminal or
    /// shipped-onward states (guarded by the order state machine).</summary>
    public async Task<Dto.OrderDto> CancelAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(orderId, cancellationToken)
                    ?? throw new DomainException("Order not found.");

        if (order.UserId != userId)
        {
            throw new DomainException("Order not found.");
        }

        if (!order.CanTransitionTo(OrderStatus.Cancelled))
        {
            throw new DomainException($"Order in status {order.Status} cannot be cancelled.");
        }

        await RestoreStockAsync(order, cancellationToken);
        order.TransitionTo(OrderStatus.Cancelled, $"customer:{userId}");

        await db.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }

    private Task<Order?> LoadAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    private async Task RestoreStockAsync(Order order, CancellationToken cancellationToken)
    {
        var variantIds = order.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (variants.TryGetValue(item.ProductVariantId, out var variant))
            {
                variant.StockQty += item.Qty;
            }
        }
    }

    private async Task<OrderAddress> ResolveAddressAsync(Guid userId, Dto.CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.AddressId is { } addressId)
        {
            var saved = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken)
                        ?? throw new DomainException("Address not found.");
            return new OrderAddress
            {
                Region = saved.Region,
                City = saved.City,
                Street = saved.Street,
                Details = saved.Details
            };
        }

        if (request.Address is { } inline)
        {
            return new OrderAddress
            {
                Region = inline.Region,
                City = inline.City,
                Street = inline.Street,
                Details = inline.Details
            };
        }

        throw new DomainException("A delivery address is required.");
    }
}
