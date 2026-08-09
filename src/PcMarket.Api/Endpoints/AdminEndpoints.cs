using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Admin;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Content;
using PcMarket.Contracts.Orders;
using PcMarket.Infrastructure.Persistence;
using PcMarket.Infrastructure.Persistence.Seed;

namespace PcMarket.Api.Endpoints;

public static class AdminEndpoints
{
    public const string Policy = "AdminPanel";

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(Policy);

        group.MapGet("/dashboard", (AdminDashboardService svc, CancellationToken ct) => svc.GetStatsAsync(ct));

        // Categories
        group.MapGet("/categories", (AdminCatalogService svc, CancellationToken ct) => svc.ListCategoriesAsync(ct));
        group.MapPost("/categories", (SaveCategoryRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveCategoryAsync(null, req, ct));
        group.MapPut("/categories/{id:guid}", (Guid id, SaveCategoryRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveCategoryAsync(id, req, ct));
        group.MapDelete("/categories/{id:guid}", async (Guid id, AdminCatalogService svc, CancellationToken ct) =>
            await svc.DeleteCategoryAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Brands
        group.MapGet("/brands", (AdminCatalogService svc, CancellationToken ct) => svc.ListBrandsAsync(ct));
        group.MapPost("/brands", (SaveBrandRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveBrandAsync(null, req, ct));
        group.MapPut("/brands/{id:guid}", (Guid id, SaveBrandRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveBrandAsync(id, req, ct));
        group.MapDelete("/brands/{id:guid}", async (Guid id, AdminCatalogService svc, CancellationToken ct) =>
            await svc.DeleteBrandAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Products
        group.MapGet("/products", (string? search, int? page, int? pageSize, AdminCatalogService svc, CancellationToken ct) =>
            svc.ListProductsAsync(search, page ?? 1, pageSize ?? 20, ct));
        group.MapGet("/products/{id:guid}", async (Guid id, AdminCatalogService svc, CancellationToken ct) =>
        {
            var product = await svc.GetProductAsync(id, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });
        group.MapPost("/products", (SaveProductRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveProductAsync(null, req, ct));
        group.MapPut("/products/{id:guid}", (Guid id, SaveProductRequest req, AdminCatalogService svc, CancellationToken ct) => svc.SaveProductAsync(id, req, ct));
        group.MapDelete("/products/{id:guid}", async (Guid id, AdminCatalogService svc, CancellationToken ct) =>
            await svc.DeleteProductAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Orders
        group.MapGet("/orders", (OrderStatus? status, string? search, int? page, int? pageSize, AdminOrderService svc, CancellationToken ct) =>
            svc.ListAsync(status, search, page ?? 1, pageSize ?? 20, ct));
        group.MapGet("/orders/{id:guid}", async (Guid id, AdminOrderService svc, CancellationToken ct) =>
        {
            var order = await svc.GetAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });
        group.MapPost("/orders/{id:guid}/advance", (Guid id, AdvanceOrderStatusRequest req, AdminOrderService svc, ICurrentUser user, CancellationToken ct) =>
            svc.AdvanceStatusAsync(id, req.ToStatus, Actor(user), ct));
        group.MapPost("/orders/{id:guid}/refund", (Guid id, AdminOrderService svc, ICurrentUser user, CancellationToken ct) =>
            svc.RefundAsync(id, Actor(user), ct));

        // Customers
        group.MapGet("/customers", (string? search, int? page, int? pageSize, AdminCustomerService svc, CancellationToken ct) =>
            svc.ListAsync(search, page ?? 1, pageSize ?? 20, ct));
        group.MapGet("/customers/lookup", async (string phone, AdminOrderService svc, CancellationToken ct) =>
        {
            var customer = await svc.LookupCustomerAsync(phone, ct);
            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });
        group.MapGet("/customers/{id:guid}", async (Guid id, AdminCustomerService svc, CancellationToken ct) =>
        {
            var customer = await svc.GetAsync(id, ct);
            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        // Content — banners
        group.MapGet("/banners", (AdminContentService svc, CancellationToken ct) => svc.ListBannersAsync(ct));
        group.MapPost("/banners", (SaveBannerRequest req, AdminContentService svc, CancellationToken ct) => svc.SaveBannerAsync(null, req, ct));
        group.MapPut("/banners/{id:guid}", (Guid id, SaveBannerRequest req, AdminContentService svc, CancellationToken ct) => svc.SaveBannerAsync(id, req, ct));
        group.MapDelete("/banners/{id:guid}", async (Guid id, AdminContentService svc, CancellationToken ct) =>
            await svc.DeleteBannerAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Content — CMS blocks
        group.MapGet("/cms-blocks", (AdminContentService svc, CancellationToken ct) => svc.ListBlocksAsync(ct));
        group.MapPost("/cms-blocks", (SaveCmsBlockRequest req, AdminContentService svc, CancellationToken ct) => svc.SaveBlockAsync(null, req, ct));
        group.MapPut("/cms-blocks/{id:guid}", (Guid id, SaveCmsBlockRequest req, AdminContentService svc, CancellationToken ct) => svc.SaveBlockAsync(id, req, ct));
        group.MapDelete("/cms-blocks/{id:guid}", async (Guid id, AdminContentService svc, CancellationToken ct) =>
            await svc.DeleteBlockAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Audit
        group.MapGet("/audit", (int? page, int? pageSize, AdminAuditService svc, CancellationToken ct) =>
            svc.ListAsync(page ?? 1, pageSize ?? 50, ct));

        // Catalog import (JSON body matching demo-catalog.json)
        group.MapPost("/catalog/import", async (HttpRequest request, PcMarketDbContext db, CancellationToken ct) =>
        {
            var result = await CatalogImporter.ImportAsync(db, request.Body, ct);
            return Results.Ok(result);
        });
    }

    private static string Actor(ICurrentUser user) => $"admin:{user.UserId}";
}
