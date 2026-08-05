namespace PcMarket.Domain.Configurator;

/// <summary>
/// A pre-selected build behind the "Ready-made assemblies" entry path. Holds component ids rather
/// than components so the bundle cannot drift out of sync with the catalog — resolve through
/// <see cref="ComponentCatalog.Resolve"/>, which throws on an unknown id, so a typo fails loudly at
/// the first call instead of silently producing a build with a part missing.
/// </summary>
/// <param name="Id">Stable identifier, used by the entry screen and the shareable link.</param>
/// <param name="Name">Display name, e.g. "Gaming PC — Intel".</param>
/// <param name="Description">One-line summary of what the build is for.</param>
/// <param name="Platform">Which entry path the bundle belongs under.</param>
/// <param name="ComponentIds">The parts, in category order.</param>
public sealed record AssemblyBundle(
    string Id,
    string Name,
    string Description,
    ComponentPlatform Platform,
    IReadOnlyList<string> ComponentIds);
