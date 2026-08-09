using PcMarket.Contracts.Common;
using PcMarket.Contracts.Orders;

namespace PcMarket.Contracts.Admin;

// ---- Dashboard ----
public sealed record DashboardStatsDto(
    int TotalOrders,
    int OrdersToday,
    int PendingOrders,
    decimal RevenueTotal,
    decimal RevenueToday,
    int ProductCount,
    int LowStockCount);

// ---- Categories ----
// Name is the canonical English value; Translations carries the other languages (see ContentLanguages).
public sealed record AdminCategoryDto(
    Guid Id, Guid? ParentId, string Name, string Slug, int SortOrder, bool IsActive, int ProductCount,
    IReadOnlyList<TranslationDto> Translations);

public sealed record SaveCategoryRequest(
    string Name, string? Slug, Guid? ParentId, int SortOrder, bool IsActive,
    IReadOnlyList<TranslationDto>? Translations = null);

// ---- Brands ----
public sealed record AdminBrandDto(Guid Id, string Name, string Slug, string? LogoUrl, int ProductCount);
public sealed record SaveBrandRequest(string Name, string? Slug, string? LogoUrl);

// ---- Products ----
public sealed record AdminProductListItemDto(
    Guid Id, string Name, string Slug, string CategoryName, string? BrandName, decimal PriceFrom, int TotalStock, bool IsActive);

public sealed record AdminVariantDto(
    Guid Id, string Sku, IReadOnlyDictionary<string, string> Attributes, decimal Price, decimal? OldPrice, int StockQty, bool IsActive);

public sealed record AdminImageDto(string Url, bool IsPrimary, int SortOrder);

public sealed record AdminProductDto(
    Guid Id, string Name, string Slug, string? Description, Guid CategoryId, Guid? BrandId,
    IReadOnlyDictionary<string, string> Specs, bool IsActive,
    IReadOnlyList<AdminVariantDto> Variants, IReadOnlyList<AdminImageDto> Images);

public sealed record SaveVariantRequest(
    Guid? Id, string Sku, IReadOnlyDictionary<string, string> Attributes, decimal Price, decimal? OldPrice, int StockQty, bool IsActive);

public sealed record SaveProductRequest(
    string Name, string? Slug, string? Description, Guid CategoryId, Guid? BrandId,
    IReadOnlyDictionary<string, string> Specs, bool IsActive,
    IReadOnlyList<SaveVariantRequest> Variants, IReadOnlyList<AdminImageDto> Images);

// ---- Orders ----
public sealed record AdminOrderListItemDto(
    Guid Id, string Number, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod PaymentMethod,
    decimal Total, int ItemCount, DateTimeOffset CreatedAt, string? CustomerPhone);

public sealed record AdminCustomerDto(Guid Id, string? Phone, string? FullName, string? Email, int OrderCount);

public sealed record AdminOrderDetailDto(OrderDto Order, AdminCustomerDto? Customer);

// ---- Customers ----

/// <summary>One row of the back-office customer list. <paramref name="Roles"/> is empty for an ordinary
/// customer and carries Admin/Manager for staff, who are listed alongside them rather than hidden — a
/// manager placing test orders should be visibly a manager, not an anonymous buyer.</summary>
public sealed record AdminCustomerListItemDto(
    Guid Id, string? Phone, string? FullName, string? Email,
    IReadOnlyList<string> Roles, bool TelegramLinked, string? Language,
    int OrderCount, decimal TotalSpent, DateTimeOffset CreatedAt);

/// <summary>A delivery address the customer saved to their account, as opposed to the one-off address
/// captured on an order.</summary>
public sealed record AdminCustomerAddressDto(
    string Region, string City, string Street, string? Details, bool IsDefault);

/// <summary>A pin the customer shared on an order. Kept per order rather than deduplicated: where someone
/// asked for delivery last time is exactly what a courier calling ahead wants to know.</summary>
public sealed record AdminCustomerLocationDto(
    Guid OrderId, string OrderNumber, string Address, double Latitude, double Longitude, DateTimeOffset CreatedAt);

public sealed record AdminCustomerDetailDto(
    AdminCustomerListItemDto Customer,
    IReadOnlyList<AdminCustomerAddressDto> Addresses,
    IReadOnlyList<AdminCustomerLocationDto> Locations,
    IReadOnlyList<AdminOrderListItemDto> Orders);

public sealed record AdvanceOrderStatusRequest(OrderStatus ToStatus);

// ---- Media ----
public sealed record MediaUploadResponse(string Url);

// ---- Audit ----
public sealed record AuditLogEntryDto(
    Guid Id, DateTimeOffset CreatedAt, string? ActorName, string Action, string EntityType, string? EntityId, string? Summary);
