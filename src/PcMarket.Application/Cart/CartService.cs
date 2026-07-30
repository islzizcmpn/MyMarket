using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Cart;
using PcMarket.Domain.Common;
using PcMarket.Domain.Ordering;

namespace PcMarket.Application.Carts;

/// <summary>Cart use-cases for authenticated users (keyed by user id) and guests (keyed by an opaque
/// token). Validates stock and snapshots the current price on every add/update.</summary>
public sealed class CartService(IApplicationDbContext db)
{
    public async Task<CartDto> GetCartAsync(Guid? userId, string? token, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(userId, token, cancellationToken)
                   ?? await CreateCartAsync(userId, token, cancellationToken);
        return await BuildDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> AddItemAsync(
        Guid? userId,
        string? token,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Qty < 1)
        {
            throw new DomainException("Quantity must be at least 1.");
        }

        var cart = await FindCartAsync(userId, token, cancellationToken)
                   ?? await CreateCartAsync(userId, token, cancellationToken);

        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId && v.IsActive, cancellationToken)
            ?? throw new DomainException("Product variant not found or unavailable.");

        var existing = cart.Items.FirstOrDefault(i => i.ProductVariantId == variant.Id);
        var desiredQty = (existing?.Qty ?? 0) + request.Qty;
        if (desiredQty > variant.StockQty)
        {
            throw new DomainException($"Only {variant.StockQty} item(s) in stock.");
        }

        if (existing is not null)
        {
            existing.Qty = desiredQty;
            existing.UnitPriceSnapshot = variant.Price;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                CartId = cart.Id,
                ProductVariantId = variant.Id,
                Qty = request.Qty,
                UnitPriceSnapshot = variant.Price
            });
        }

        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(
        Guid? userId,
        string? token,
        Guid itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(userId, token, cancellationToken)
                   ?? throw new DomainException("Cart not found.");

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId)
                   ?? throw new DomainException("Cart item not found.");

        if (request.Qty <= 0)
        {
            cart.Items.Remove(item);
        }
        else
        {
            var variant = await db.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken)
                ?? throw new DomainException("Product variant not found.");

            if (request.Qty > variant.StockQty)
            {
                throw new DomainException($"Only {variant.StockQty} item(s) in stock.");
            }

            item.Qty = request.Qty;
            item.UnitPriceSnapshot = variant.Price;
        }

        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(
        Guid? userId,
        string? token,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var cart = await FindCartAsync(userId, token, cancellationToken)
                   ?? throw new DomainException("Cart not found.");

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
        {
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await BuildDtoAsync(cart, cancellationToken);
    }

    /// <summary>Merges a guest cart (by token) into the authenticated user's cart, then deletes the guest cart.</summary>
    public async Task<CartDto> MergeAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var guestCart = await FindByTokenAsync(token, cancellationToken);
        var userCart = await FindByUserAsync(userId, cancellationToken)
                       ?? await CreateCartAsync(userId, null, cancellationToken);

        if (guestCart is null || guestCart.Id == userCart.Id)
        {
            return await BuildDtoAsync(userCart, cancellationToken);
        }

        var variantIds = guestCart.Items.Select(i => i.ProductVariantId).ToList();
        var stockByVariant = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.StockQty, cancellationToken);

        foreach (var guestItem in guestCart.Items)
        {
            if (!stockByVariant.TryGetValue(guestItem.ProductVariantId, out var stock))
            {
                continue;
            }

            var existing = userCart.Items.FirstOrDefault(i => i.ProductVariantId == guestItem.ProductVariantId);
            if (existing is not null)
            {
                existing.Qty = Math.Min(existing.Qty + guestItem.Qty, stock);
            }
            else
            {
                userCart.Items.Add(new CartItem
                {
                    CartId = userCart.Id,
                    ProductVariantId = guestItem.ProductVariantId,
                    Qty = Math.Min(guestItem.Qty, stock),
                    UnitPriceSnapshot = guestItem.UnitPriceSnapshot
                });
            }
        }

        db.Carts.Remove(guestCart);
        userCart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(userCart, cancellationToken);
    }

    private async Task<Cart?> FindCartAsync(Guid? userId, string? token, CancellationToken cancellationToken) =>
        userId is not null
            ? await FindByUserAsync(userId.Value, cancellationToken)
            : token is not null
                ? await FindByTokenAsync(token, cancellationToken)
                : null;

    private Task<Cart?> FindByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private Task<Cart?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == token, cancellationToken);

    private async Task<Cart> CreateCartAsync(Guid? userId, string? token, CancellationToken cancellationToken)
    {
        var cart = new Cart
        {
            UserId = userId,
            Token = userId is null ? token ?? Guid.NewGuid().ToString("N") : null
        };

        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task<CartDto> BuildDtoAsync(Cart cart, CancellationToken cancellationToken)
    {
        var variantIds = cart.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Include(v => v.Product)
            .ThenInclude(p => p.Images)
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var items = cart.Items.Select(i =>
        {
            variants.TryGetValue(i.ProductVariantId, out var variant);
            var product = variant?.Product;
            var image = product?.Images.FirstOrDefault(x => x.IsPrimary)
                        ?? product?.Images.OrderBy(x => x.SortOrder).FirstOrDefault();

            return new CartItemDto(
                i.Id,
                i.ProductVariantId,
                product?.Name ?? string.Empty,
                variant?.Sku ?? string.Empty,
                i.UnitPriceSnapshot,
                i.Qty,
                i.UnitPriceSnapshot * i.Qty,
                variant?.StockQty ?? 0,
                image?.Url);
        }).ToList();

        return new CartDto(
            cart.Id,
            cart.Token,
            items,
            items.Sum(x => x.LineTotal),
            items.Sum(x => x.Qty));
    }
}
