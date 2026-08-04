using System.Collections.Concurrent;

namespace PcMarket.Web.Services;

/// <summary>
/// Resolves the storefront's local artwork in <c>wwwroot/images/home</c>, returning <see langword="null"/>
/// for anything that is not actually on disk so callers can fall back to their existing placeholder
/// rather than emitting a broken image.
/// <para>
/// The art is dropped into the folder by hand and the set is not final, so every reference is
/// treated as optional. Checking once here — instead of letting the browser 404 — is what lets the
/// monogram rings, the drawn SVG and the text chips stay in place for the spots that have no file yet.
/// </para>
/// </summary>
public sealed class HomeImages(IWebHostEnvironment environment)
{
    private const string Folder = "images/home";

    // The file set cannot change without a redeploy, so a lookup is only ever paid once per name.
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Web path for <paramref name="fileName"/>, or <see langword="null"/> if it is missing.</summary>
    public string? Path(string fileName) => _cache.GetOrAdd(fileName, name =>
        environment.WebRootFileProvider.GetFileInfo($"{Folder}/{name}").Exists ? $"/{Folder}/{name}" : null);

    /// <summary>
    /// First of <paramref name="fileNames"/> that exists, or <see langword="null"/> if none do. Lets a
    /// slot name its preferred file and a stand-in: <c>First("hero-cyberpunk.jpg", "hero-night.jpg")</c>.
    /// </summary>
    public string? First(params string[] fileNames)
    {
        foreach (var name in fileNames)
        {
            if (Path(name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Every name that exists, in the order given. Missing entries are dropped silently.</summary>
    public IReadOnlyList<string> Existing(params string[] fileNames) =>
        fileNames.Select(Path).OfType<string>().ToList();
}
