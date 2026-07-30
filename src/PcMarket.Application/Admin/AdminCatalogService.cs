using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Audit;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Catalog;
using PcMarket.Application.Localization;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Common;
using PcMarket.Domain.Catalog;
using PcMarket.Domain.Common;

namespace PcMarket.Application.Admin;

/// <summary>Back-office CRUD for categories, brands, and products (with variants + images). Every mutation is
/// written to the audit trail.</summary>
public sealed class AdminCatalogService(
    IApplicationDbContext db,
    IAuditLogger audit,
    TranslationWriter translations,
    ICacheService cache)
{
    // ---- Categories ----
    public async Task<IReadOnlyList<AdminCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await db.Categories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.ParentId, c.Name, c.Slug, c.SortOrder, c.IsActive, ProductCount = c.Products.Count
            })
            .ToListAsync(ct);

        var byCategory = await translations.ListManyAsync(
            TranslatableEntities.Category, [.. categories.Select(c => c.Id)], ct);

        return [.. categories.Select(c => new AdminCategoryDto(
            c.Id, c.ParentId, c.Name, c.Slug, c.SortOrder, c.IsActive, c.ProductCount, [.. byCategory[c.Id]]))];
    }

    public async Task<AdminCategoryDto> SaveCategoryAsync(Guid? id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        var category = id is null ? new Category { Name = request.Name, Slug = string.Empty } : await FindCategoryAsync(id.Value, ct);
        category.Name = request.Name;
        category.Slug = ResolveSlug(request.Slug, request.Name);
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        if (id is null)
        {
            db.Categories.Add(category);
        }
        else
        {
            category.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await translations.ReplaceAsync(TranslatableEntities.Category, category.Id, request.Translations, ct);

        await db.SaveChangesAsync(ct);
        await InvalidateCategoryTreeAsync(ct);
        await audit.LogAsync(id is null ? "category.create" : "category.update", "Category", category.Id.ToString(), category.Name, ct);

        var saved = await translations.ListAsync(TranslatableEntities.Category, category.Id, ct);
        return new AdminCategoryDto(
            category.Id, category.ParentId, category.Name, category.Slug, category.SortOrder, category.IsActive, 0, saved);
    }

    /// <summary>The storefront caches the category tree per language for ten minutes. Without this an editor's
    /// change — a renamed category or a newly added translation — would look like it had not been saved.</summary>
    private async Task InvalidateCategoryTreeAsync(CancellationToken ct)
    {
        foreach (var culture in LanguageCodes.Supported)
        {
            await cache.RemoveAsync(CatalogCacheKeys.CategoryTree(culture), ct);
        }
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return false;
        }

        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct) || await db.Categories.AnyAsync(c => c.ParentId == id, ct))
        {
            throw new DomainException("Cannot delete a category that has products or subcategories.");
        }

        db.Categories.Remove(category);
        // ContentTranslations has no foreign key to its owner, so the rows have to be cleared explicitly.
        await translations.RemoveAllAsync(TranslatableEntities.Category, id, ct);
        await db.SaveChangesAsync(ct);
        await InvalidateCategoryTreeAsync(ct);
        await audit.LogAsync("category.delete", "Category", id.ToString(), category.Name, ct);
        return true;
    }

    // ---- Brands ----
    public async Task<IReadOnlyList<AdminBrandDto>> ListBrandsAsync(CancellationToken ct = default) =>
        await db.Brands
            .OrderBy(b => b.Name)
            .Select(b => new AdminBrandDto(b.Id, b.Name, b.Slug, b.LogoUrl, b.Products.Count))
            .ToListAsync(ct);

    public async Task<AdminBrandDto> SaveBrandAsync(Guid? id, SaveBrandRequest request, CancellationToken ct = default)
    {
        var brand = id is null ? new Brand { Name = request.Name, Slug = string.Empty } : await FindBrandAsync(id.Value, ct);
        brand.Name = request.Name;
        brand.Slug = ResolveSlug(request.Slug, request.Name);
        brand.LogoUrl = request.LogoUrl;

        if (id is null)
        {
            db.Brands.Add(brand);
        }
        else
        {
            brand.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(id is null ? "brand.create" : "brand.update", "Brand", brand.Id.ToString(), brand.Name, ct);
        return new AdminBrandDto(brand.Id, brand.Name, brand.Slug, brand.LogoUrl, 0);
    }

    public async Task<bool> DeleteBrandAsync(Guid id, CancellationToken ct = default)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null)
        {
            return false;
        }

        if (await db.Products.AnyAsync(p => p.BrandId == id, ct))
        {
            throw new DomainException("Cannot delete a brand that still has products.");
        }

        db.Brands.Remove(brand);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("brand.delete", "Brand", id.ToString(), brand.Name, ct);
        return true;
    }

    // ---- Products ----
    public async Task<PagedResult<AdminProductListItemDto>> ListProductsAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Variants).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Slug.Contains(term));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var mapped = items.Select(p => new AdminProductListItemDto(
            p.Id, p.Name, p.Slug, p.Category.Name, p.Brand != null ? p.Brand.Name : null,
            p.Variants.Count > 0 ? p.Variants.Min(v => v.Price) : 0,
            p.Variants.Sum(v => v.StockQty), p.IsActive)).ToList();

        return new PagedResult<AdminProductListItemDto>(mapped, page, pageSize, total);
    }

    public async Task<AdminProductDto?> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? null : ToDto(product);
    }

    public async Task<AdminProductDto> SaveProductAsync(Guid? id, SaveProductRequest request, CancellationToken ct = default)
    {
        Product product;
        if (id is null)
        {
            product = new Product { Name = request.Name, Slug = string.Empty, CategoryId = request.CategoryId };
            db.Products.Add(product);
        }
        else
        {
            product = await db.Products.Include(p => p.Variants).Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new DomainException("Product not found.");
        }

        product.Name = request.Name;
        product.Slug = ResolveSlug(request.Slug, request.Name);
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.Specs = new Dictionary<string, string>(request.Specs);
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        SyncVariants(product, request.Variants);
        SyncImages(product, request.Images);

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(id is null ? "product.create" : "product.update", "Product", product.Id.ToString(), product.Name, ct);
        return ToDto(product);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return false;
        }

        if (await db.OrderItems.AnyAsync(oi => db.ProductVariants.Any(v => v.ProductId == id && v.Id == oi.ProductVariantId), ct))
        {
            // Preserve history: soft-deactivate products that appear in orders instead of hard-deleting.
            product.IsActive = false;
            await db.SaveChangesAsync(ct);
            await audit.LogAsync("product.deactivate", "Product", id.ToString(), product.Name, ct);
            return true;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("product.delete", "Product", id.ToString(), product.Name, ct);
        return true;
    }

    private void SyncVariants(Product product, IReadOnlyList<SaveVariantRequest> incoming)
    {
        var keepIds = incoming.Where(v => v.Id is not null).Select(v => v.Id!.Value).ToHashSet();
        foreach (var existing in product.Variants.Where(v => !keepIds.Contains(v.Id)).ToList())
        {
            product.Variants.Remove(existing);
        }

        foreach (var v in incoming)
        {
            var variant = v.Id is not null ? product.Variants.FirstOrDefault(x => x.Id == v.Id) : null;
            if (variant is null)
            {
                variant = new ProductVariant { Sku = v.Sku };
                product.Variants.Add(variant);
            }

            variant.Sku = v.Sku;
            variant.Attributes = new Dictionary<string, string>(v.Attributes);
            variant.Price = v.Price;
            variant.OldPrice = v.OldPrice;
            variant.StockQty = v.StockQty;
            variant.IsActive = v.IsActive;
        }
    }

    private static void SyncImages(Product product, IReadOnlyList<AdminImageDto> incoming)
    {
        product.Images.Clear();
        var ordered = incoming.OrderBy(i => i.SortOrder).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            product.Images.Add(new ProductImage
            {
                ProductId = product.Id,
                Url = ordered[i].Url,
                SortOrder = i,
                IsPrimary = ordered[i].IsPrimary || (i == 0 && ordered.All(x => !x.IsPrimary))
            });
        }
    }

    private static AdminProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.CategoryId, p.BrandId, p.Specs, p.IsActive,
        p.Variants.Select(v => new AdminVariantDto(v.Id, v.Sku, v.Attributes, v.Price, v.OldPrice, v.StockQty, v.IsActive)).ToList(),
        p.Images.OrderBy(i => i.SortOrder).Select(i => new AdminImageDto(i.Url, i.IsPrimary, i.SortOrder)).ToList());

    private async Task<Category> FindCategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new DomainException("Category not found.");

    private async Task<Brand> FindBrandAsync(Guid id, CancellationToken ct) =>
        await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new DomainException("Brand not found.");

    private static string ResolveSlug(string? provided, string name) =>
        string.IsNullOrWhiteSpace(provided) ? SlugGenerator.Generate(name) : SlugGenerator.Generate(provided);
}
