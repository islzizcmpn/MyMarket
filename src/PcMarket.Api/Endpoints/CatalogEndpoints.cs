using PcMarket.Application.Catalog;
using PcMarket.Contracts.Catalog;

namespace PcMarket.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog").WithTags("Catalog");

        group.MapGet("/categories", async (CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetCategoryTreeAsync(ct)));

        group.MapGet("/brands", async (CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetBrandsAsync(ct)));

        group.MapGet("/products", async (
            CatalogService catalog,
            string? categorySlug,
            string? brandSlug,
            decimal? minPrice,
            decimal? maxPrice,
            ProductSort? sort,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
            Results.Ok(await catalog.GetProductsAsync(
                categorySlug, brandSlug, minPrice, maxPrice, sort ?? ProductSort.Newest, page ?? 1, pageSize ?? 20, ct)));

        group.MapGet("/products/{slug}", async (string slug, CatalogService catalog, CancellationToken ct) =>
        {
            var product = await catalog.GetProductBySlugAsync(slug, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapGet("/search", async (
            string? q,
            int? page,
            int? pageSize,
            CatalogService catalog,
            CancellationToken ct) =>
            Results.Ok(await catalog.SearchAsync(q ?? string.Empty, page ?? 1, pageSize ?? 20, ct)));
    }
}
