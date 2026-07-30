using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Catalog;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Localization;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Common;
using PcMarket.Domain.Catalog;

namespace PcMarket.Application.Catalog;

/// <summary>Read-side use-cases for the storefront catalog: category tree, brand list, filtered/paged
/// product listings, product detail, and full-text search. Stable reads are cached in Redis.</summary>
public sealed class CatalogService(
    IApplicationDbContext db,
    ICacheService cache,
    IProductSearchQuery search,
    ILanguageContext language,
    TranslationReader translations)
{
    private const int MaxPageSize = 60;
    private static readonly TimeSpan ReferenceTtl = TimeSpan.FromMinutes(10);

    private sealed record CategoryFlat(Guid Id, Guid? ParentId, string Name, string Slug, int SortOrder);

    /// <summary>The cache key carries the language: the tree is translated before it is cached, so a shared
    /// key would serve the first caller's language to everyone until the entry expired.</summary>
    public Task<IReadOnlyList<CategoryNodeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default) =>
        cache.GetOrSetAsync<IReadOnlyList<CategoryNodeDto>>(
            CatalogCacheKeys.CategoryTree(language.Culture),
            async ct =>
            {
                var flats = await LoadCategoriesAsync(ct);
                var names = await translations.LoadAsync(
                    TranslatableEntities.Category, [.. flats.Select(c => c.Id)], ct);

                // Re-sorted after translating: the database ordered by the canonical name, which is not the
                // alphabetical order of the names actually being shown.
                var localized = flats
                    .Select(c => c with { Name = names.Resolve(c.Id, nameof(Category.Name), c.Name) })
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return BuildTree(localized, null);
            },
            ReferenceTtl,
            cancellationToken);

    public Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        cache.GetOrSetAsync<IReadOnlyList<BrandDto>>(
            CatalogCacheKeys.Brands,
            async ct => await db.Brands
                .OrderBy(b => b.Name)
                .Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.LogoUrl))
                .ToListAsync(ct),
            ReferenceTtl,
            cancellationToken);

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        string? categorySlug,
        string? brandSlug,
        decimal? minPrice,
        decimal? maxPrice,
        ProductSort sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = db.Products.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var categoryIds = await ResolveCategoryAndDescendantsAsync(categorySlug, cancellationToken);
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(brandSlug))
        {
            query = query.Where(p => p.Brand != null && p.Brand.Slug == brandSlug);
        }

        if (minPrice is not null)
        {
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Price >= minPrice));
        }

        if (maxPrice is not null)
        {
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Price <= maxPrice));
        }

        var total = await query.LongCountAsync(cancellationToken);

        query = sort switch
        {
            ProductSort.PriceAsc => query.OrderBy(p => p.Variants.Where(v => v.IsActive).Min(v => (decimal?)v.Price)),
            ProductSort.PriceDesc => query.OrderByDescending(p => p.Variants.Where(v => v.IsActive).Max(v => (decimal?)v.Price)),
            ProductSort.NameAsc => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>(
            products.Select(p => p.ToListItemDto()).ToList(), page, pageSize, total);
    }

    public async Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, cancellationToken);

        return product?.ToDetailDto();
    }

    /// <summary>Product detail by id. Used by clients whose navigation carries ids rather than slugs — the
    /// Telegram bot, whose inline-button payloads are capped at 64 bytes.</summary>
    public async Task<ProductDetailDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        return product?.ToDetailDto();
    }

    public async Task<PagedResult<ProductListItemDto>> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        if (string.IsNullOrWhiteSpace(term))
        {
            return new PagedResult<ProductListItemDto>([], page, pageSize, 0);
        }

        var (ids, total) = await search.SearchAsync(term, page, pageSize, cancellationToken);
        if (ids.Count == 0)
        {
            return new PagedResult<ProductListItemDto>([], page, pageSize, total);
        }

        var products = await db.Products
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        // Preserve the relevance order returned by the search query.
        var ordered = ids
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Select(p => p!.ToListItemDto())
            .ToList();

        return new PagedResult<ProductListItemDto>(ordered, page, pageSize, total);
    }

    private async Task<List<CategoryFlat>> LoadCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryFlat(c.Id, c.ParentId, c.Name, c.Slug, c.SortOrder))
            .ToListAsync(cancellationToken);

    private async Task<HashSet<Guid>> ResolveCategoryAndDescendantsAsync(string slug, CancellationToken cancellationToken)
    {
        var flats = await LoadCategoriesAsync(cancellationToken);
        var root = flats.FirstOrDefault(c => c.Slug == slug);
        var result = new HashSet<Guid>();
        if (root is null)
        {
            return result;
        }

        var byParent = flats.ToLookup(c => c.ParentId);
        var stack = new Stack<Guid>();
        stack.Push(root.Id);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id))
            {
                continue;
            }

            foreach (var child in byParent[id])
            {
                stack.Push(child.Id);
            }
        }

        return result;
    }

    private static List<CategoryNodeDto> BuildTree(List<CategoryFlat> flats, Guid? parentId) =>
        flats
            .Where(c => c.ParentId == parentId)
            .Select(c => new CategoryNodeDto(c.Id, c.Name, c.Slug, c.SortOrder, BuildTree(flats, c.Id)))
            .ToList();

    private static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (page < 1 ? 1 : page, pageSize is < 1 or > MaxPageSize ? 20 : pageSize);
}
