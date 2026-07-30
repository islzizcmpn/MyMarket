namespace PcMarket.Contracts.Cart;

public sealed record CartItemDto(
    Guid Id,
    Guid ProductVariantId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Qty,
    decimal LineTotal,
    int StockQty,
    string? PrimaryImageUrl);

public sealed record CartDto(
    Guid Id,
    string? Token,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal,
    int TotalQty);

public sealed record AddCartItemRequest(Guid ProductVariantId, int Qty);

public sealed record UpdateCartItemRequest(int Qty);
