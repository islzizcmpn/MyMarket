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

public sealed record AdvanceOrderStatusRequest(OrderStatus ToStatus);

// ---- Media ----
public sealed record MediaUploadResponse(string Url);

// ---- Audit ----
public sealed record AuditLogEntryDto(
    Guid Id, DateTimeOffset CreatedAt, string? ActorName, string Action, string EntityType, string? EntityId, string? Summary);
