using System.Net.Http.Headers;
using System.Text;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Common;
using PcMarket.Contracts.Content;
using PcMarket.Contracts.Orders;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the back-office admin endpoints (role-guarded on the server).</summary>
public sealed class AdminApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<DashboardStatsDto> GetDashboardAsync(CancellationToken ct = default) =>
        GetAsync<DashboardStatsDto>("admin/dashboard", ct);

    // Categories
    public Task<IReadOnlyList<AdminCategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminCategoryDto>>("admin/categories", ct);
    public Task<AdminCategoryDto> CreateCategoryAsync(SaveCategoryRequest req, CancellationToken ct = default) =>
        PostAsync<SaveCategoryRequest, AdminCategoryDto>("admin/categories", req, ct);
    public Task<AdminCategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest req, CancellationToken ct = default) =>
        PutAsync<SaveCategoryRequest, AdminCategoryDto>($"admin/categories/{id}", req, ct);
    public Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"admin/categories/{id}", ct);

    // Brands
    public Task<IReadOnlyList<AdminBrandDto>> ListBrandsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminBrandDto>>("admin/brands", ct);
    public Task<AdminBrandDto> CreateBrandAsync(SaveBrandRequest req, CancellationToken ct = default) =>
        PostAsync<SaveBrandRequest, AdminBrandDto>("admin/brands", req, ct);
    public Task<AdminBrandDto> UpdateBrandAsync(Guid id, SaveBrandRequest req, CancellationToken ct = default) =>
        PutAsync<SaveBrandRequest, AdminBrandDto>($"admin/brands/{id}", req, ct);
    public Task<bool> DeleteBrandAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"admin/brands/{id}", ct);

    // Products
    public Task<PagedResult<AdminProductListItemDto>> ListProductsAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = $"admin/products?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<AdminProductListItemDto>>(query, ct);
    }
    public Task<AdminProductDto?> GetProductAsync(Guid id, CancellationToken ct = default) =>
        GetOrDefaultAsync<AdminProductDto>($"admin/products/{id}", ct);
    public Task<AdminProductDto> CreateProductAsync(SaveProductRequest req, CancellationToken ct = default) =>
        PostAsync<SaveProductRequest, AdminProductDto>("admin/products", req, ct);
    public Task<AdminProductDto> UpdateProductAsync(Guid id, SaveProductRequest req, CancellationToken ct = default) =>
        PutAsync<SaveProductRequest, AdminProductDto>($"admin/products/{id}", req, ct);
    public Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"admin/products/{id}", ct);

    // Orders
    public Task<PagedResult<AdminOrderListItemDto>> ListOrdersAsync(OrderStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = $"admin/orders?page={page}&pageSize={pageSize}";
        if (status is not null) query += $"&status={status}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<AdminOrderListItemDto>>(query, ct);
    }
    public Task<AdminOrderDetailDto?> GetOrderAsync(Guid id, CancellationToken ct = default) =>
        GetOrDefaultAsync<AdminOrderDetailDto>($"admin/orders/{id}", ct);
    public Task<AdminOrderDetailDto> AdvanceOrderAsync(Guid id, OrderStatus toStatus, CancellationToken ct = default) =>
        PostAsync<AdvanceOrderStatusRequest, AdminOrderDetailDto>($"admin/orders/{id}/advance", new AdvanceOrderStatusRequest(toStatus), ct);
    public Task<AdminOrderDetailDto> RefundOrderAsync(Guid id, CancellationToken ct = default) =>
        PostAsync<AdminOrderDetailDto>($"admin/orders/{id}/refund", ct);
    public Task<AdminCustomerDto?> LookupCustomerAsync(string phone, CancellationToken ct = default) =>
        GetOrDefaultAsync<AdminCustomerDto>($"admin/customers/lookup?phone={Uri.EscapeDataString(phone)}", ct);

    // Content — banners
    public Task<IReadOnlyList<AdminBannerDto>> ListBannersAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminBannerDto>>("admin/banners", ct);
    public Task<AdminBannerDto> CreateBannerAsync(SaveBannerRequest req, CancellationToken ct = default) =>
        PostAsync<SaveBannerRequest, AdminBannerDto>("admin/banners", req, ct);
    public Task<AdminBannerDto> UpdateBannerAsync(Guid id, SaveBannerRequest req, CancellationToken ct = default) =>
        PutAsync<SaveBannerRequest, AdminBannerDto>($"admin/banners/{id}", req, ct);
    public Task<bool> DeleteBannerAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"admin/banners/{id}", ct);

    // Content — CMS blocks
    public Task<IReadOnlyList<AdminCmsBlockDto>> ListBlocksAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<AdminCmsBlockDto>>("admin/cms-blocks", ct);
    public Task<AdminCmsBlockDto> CreateBlockAsync(SaveCmsBlockRequest req, CancellationToken ct = default) =>
        PostAsync<SaveCmsBlockRequest, AdminCmsBlockDto>("admin/cms-blocks", req, ct);
    public Task<AdminCmsBlockDto> UpdateBlockAsync(Guid id, SaveCmsBlockRequest req, CancellationToken ct = default) =>
        PutAsync<SaveCmsBlockRequest, AdminCmsBlockDto>($"admin/cms-blocks/{id}", req, ct);
    public Task<bool> DeleteBlockAsync(Guid id, CancellationToken ct = default) => DeleteAsync($"admin/cms-blocks/{id}", ct);

    // Audit
    public Task<PagedResult<AuditLogEntryDto>> ListAuditAsync(int page, int pageSize, CancellationToken ct = default) =>
        GetAsync<PagedResult<AuditLogEntryDto>>($"admin/audit?page={page}&pageSize={pageSize}", ct);

    // Catalog import (raw JSON)
    public Task ImportCatalogAsync(string json, CancellationToken ct = default) =>
        PostContentAsync("admin/catalog/import", new StringContent(json, Encoding.UTF8, "application/json"), ct);

    // Media upload
    public Task<MediaUploadResponse> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return PostContentAsync<MediaUploadResponse>("admin/media", form, ct);
    }
}
