namespace PcMarket.Application.Catalog;

/// <summary>Cache keys for the catalog read side. Centralized because the reader and the back-office
/// invalidation have to agree exactly — the category tree is cached per language, and a key built in one place
/// but not the other is invisible until someone notices stale text in one language only.</summary>
public static class CatalogCacheKeys
{
    public static string CategoryTree(string culture) => $"catalog:category-tree:{culture}";

    public const string Brands = "catalog:brands";
}
