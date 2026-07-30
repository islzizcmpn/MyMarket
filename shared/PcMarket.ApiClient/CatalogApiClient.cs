using System.Globalization;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Common;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the catalog endpoints.</summary>
public sealed class CatalogApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<IReadOnlyList<CategoryNodeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<CategoryNodeDto>>("catalog/categories", cancellationToken);

    public Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<BrandDto>>("catalog/brands", cancellationToken);

    public Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        string? categorySlug = null,
        string? brandSlug = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        ProductSort sort = ProductSort.Newest,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"sort={sort}", $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(categorySlug)) query.Add($"categorySlug={Uri.EscapeDataString(categorySlug)}");
        if (!string.IsNullOrWhiteSpace(brandSlug)) query.Add($"brandSlug={Uri.EscapeDataString(brandSlug)}");
        if (minPrice is not null) query.Add($"minPrice={minPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        if (maxPrice is not null) query.Add($"maxPrice={maxPrice.Value.ToString(CultureInfo.InvariantCulture)}");

        return GetAsync<PagedResult<ProductListItemDto>>($"catalog/products?{string.Join('&', query)}", cancellationToken);
    }

    public Task<ProductDetailDto?> GetProductAsync(string slug, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<ProductDetailDto>($"catalog/products/{Uri.EscapeDataString(slug)}", cancellationToken);

    public Task<PagedResult<ProductListItemDto>> SearchAsync(
        string query, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<ProductListItemDto>>(
            $"catalog/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}", cancellationToken);
}
