namespace PcMarket.Mobile.Services;

/// <summary>
/// Resolves the storefront's decorative artwork to absolute URLs and to cached image sources.
/// <para>
/// The files live in <c>PcMarket.Web/wwwroot/images</c> and are served by the storefront, not by the API
/// and not out of the package. Nothing here checks whether a file exists — the app has no way to ask —
/// so a missing one loads nothing and leaves whatever the container is painted with showing. Every
/// remote image therefore sits on a token-coloured panel, which is the fallback rather than an error path.
/// </para>
/// </summary>
public static class Artwork
{
    /// <summary>Web root the storefront serves the image folder from.</summary>
    private const string Folder = "images";

    /// <summary>Sub-folder holding the home-page art set.</summary>
    private const string HomeFolder = "home";

#if DEBUG
    // Short in Debug so a file replaced on the storefront shows up on the next visit to a screen.
    private static readonly TimeSpan CacheValidity = TimeSpan.FromMinutes(5);
#else
    // The set only changes when the storefront is redeployed, so a repeat visit should never refetch.
    private static readonly TimeSpan CacheValidity = TimeSpan.FromDays(30);
#endif

    /// <summary>
    /// Categories whose ring artwork is not named after their slug. Mirrors the stand-in map in the
    /// storefront's <c>Home.razor</c>, so both clients show the same photograph for a category the art
    /// set has no dedicated file for. A slug in neither the map nor the file set resolves to a URL that
    /// simply does not load, and the tile keeps its token surface.
    /// </summary>
    private static readonly Dictionary<string, string> CategoryStandIns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["computers"] = "feat-case-1.jpg",
        };

    /// <summary>Hero banner behind the home page's headline.</summary>
    public static string Banner => Home("banner-dark.jpg");

    /// <summary>
    /// Absolute URL for a path relative to the storefront's image folder, so
    /// <c>"home/banner-dark.jpg"</c> becomes <c>{media root}/images/home/banner-dark.jpg</c>. A path that
    /// is already absolute is returned untouched, which is what lets product imagery — served from
    /// wherever the catalogue says — go through the same call as the local art set.
    /// </summary>
    public static string Url(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var path = relativePath.Trim();

        return Uri.IsWellFormedUriString(path, UriKind.Absolute)
            ? path
            : $"{AppConfig.MediaRootUrl}/{Folder}/{path.TrimStart('/')}";
    }

    /// <summary>Tile artwork for a category, by the slug the catalogue API returns.</summary>
    public static string Category(string slug) =>
        Home(CategoryStandIns.TryGetValue(slug, out var standIn) ? standIn : $"cat-{slug}.jpg");

    /// <summary>
    /// Cached image source for a URL, or <see langword="null"/> when there is nothing to fetch.
    /// <para>
    /// Returning <see langword="null"/> rather than a placeholder is deliberate: an <c>Image</c> with no
    /// source draws nothing, so the token-coloured panel behind it is what remains visible. That is the
    /// same end state as a request that fails, which keeps the missing and the broken case identical.
    /// </para>
    /// </summary>
    public static ImageSource? Source(string? url)
    {
        var resolved = Url(url ?? string.Empty);

        // UriImageSource throws on anything that is not an absolute URI, and a relative path can still
        // arrive here from catalogue data, so the parse is what decides rather than the caller.
        return Uri.TryCreate(resolved, UriKind.Absolute, out var uri)
            ? new UriImageSource { Uri = uri, CachingEnabled = true, CacheValidity = CacheValidity }
            : null;
    }

    private static string Home(string fileName) => Url($"{HomeFolder}/{fileName}");
}
