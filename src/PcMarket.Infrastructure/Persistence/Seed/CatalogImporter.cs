using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PcMarket.Domain.Catalog;
using PcMarket.Domain.Common;

namespace PcMarket.Infrastructure.Persistence.Seed;

/// <summary>Bulk-loads a catalog from a JSON document. Idempotent: existing brands/categories
/// (by slug) and products (by slug) are skipped, so re-running only inserts what's new.</summary>
public static class CatalogImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<CatalogImportResult> ImportAsync(
        PcMarketDbContext db,
        Stream json,
        CancellationToken cancellationToken = default)
    {
        var seed = await JsonSerializer.DeserializeAsync<CatalogSeed>(json, JsonOptions, cancellationToken)
                   ?? new CatalogSeed();

        var brandsAdded = await ImportBrandsAsync(db, seed, cancellationToken);
        var categoriesAdded = await ImportCategoriesAsync(db, seed, cancellationToken);
        var productsAdded = await ImportProductsAsync(db, seed, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return new CatalogImportResult(brandsAdded, categoriesAdded, productsAdded);
    }

    private static async Task<int> ImportBrandsAsync(PcMarketDbContext db, CatalogSeed seed, CancellationToken ct)
    {
        var existing = await db.Brands.Select(b => b.Slug).ToHashSetAsync(ct);
        var added = 0;
        foreach (var b in seed.Brands)
        {
            var slug = Slug(b.Slug, b.Name);
            if (!existing.Add(slug))
            {
                continue;
            }

            db.Brands.Add(new Brand { Name = b.Name, Slug = slug, LogoUrl = b.LogoUrl });
            added++;
        }

        return added;
    }

    private static async Task<int> ImportCategoriesAsync(PcMarketDbContext db, CatalogSeed seed, CancellationToken ct)
    {
        var bySlug = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);
        var added = 0;

        // First pass: create missing categories.
        foreach (var c in seed.Categories)
        {
            var slug = Slug(c.Slug, c.Name);
            if (bySlug.ContainsKey(slug))
            {
                continue;
            }

            var category = new Category { Name = c.Name, Slug = slug, SortOrder = c.SortOrder };
            db.Categories.Add(category);
            bySlug[slug] = category;
            added++;
        }

        // Second pass: link parents once every category exists in the map.
        foreach (var c in seed.Categories)
        {
            if (string.IsNullOrWhiteSpace(c.ParentSlug))
            {
                continue;
            }

            var slug = Slug(c.Slug, c.Name);
            if (bySlug.TryGetValue(slug, out var child)
                && bySlug.TryGetValue(c.ParentSlug, out var parent)
                && child.ParentId is null)
            {
                child.Parent = parent;
            }
        }

        return added;
    }

    private static async Task<int> ImportProductsAsync(PcMarketDbContext db, CatalogSeed seed, CancellationToken ct)
    {
        var existingProducts = await db.Products.Select(p => p.Slug).ToHashSetAsync(ct);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);
        var brands = await db.Brands.ToDictionaryAsync(b => b.Slug, ct);

        // Include not-yet-saved entities added earlier in this import.
        foreach (var tracked in db.ChangeTracker.Entries<Category>())
        {
            categories[tracked.Entity.Slug] = tracked.Entity;
        }

        foreach (var tracked in db.ChangeTracker.Entries<Brand>())
        {
            brands[tracked.Entity.Slug] = tracked.Entity;
        }

        var added = 0;
        foreach (var p in seed.Products)
        {
            var slug = Slug(p.Slug, p.Name);
            if (!existingProducts.Add(slug) || !categories.TryGetValue(p.CategorySlug, out var category))
            {
                continue;
            }

            var product = new Product
            {
                Name = p.Name,
                Slug = slug,
                Description = p.Description,
                Specs = p.Specs,
                Category = category
            };

            if (!string.IsNullOrWhiteSpace(p.BrandSlug) && brands.TryGetValue(p.BrandSlug, out var brand))
            {
                product.Brand = brand;
            }

            foreach (var v in p.Variants)
            {
                product.Variants.Add(new ProductVariant
                {
                    Sku = v.Sku,
                    Attributes = v.Attributes,
                    Price = v.Price,
                    OldPrice = v.OldPrice,
                    StockQty = v.StockQty
                });
            }

            for (var i = 0; i < p.Images.Count; i++)
            {
                product.Images.Add(new ProductImage
                {
                    Url = p.Images[i],
                    SortOrder = i,
                    IsPrimary = i == 0
                });
            }

            db.Products.Add(product);
            added++;
        }

        return added;
    }

    private static string Slug(string? provided, string name) =>
        string.IsNullOrWhiteSpace(provided) ? SlugGenerator.Generate(name) : provided;
}
