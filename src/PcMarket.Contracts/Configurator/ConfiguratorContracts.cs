namespace PcMarket.Contracts.Configurator;

/// <summary>
/// The three entry paths of the PC configurator (Phase 19).
/// <para>
/// Lives in Contracts rather than in the Web project because the picker screen will need the parts
/// list, and the parts live in <c>PcMarket.Domain.Configurator</c> — which clients are not allowed
/// to reference (the architecture keeps Domain backend-only, with Contracts as the single DTO source
/// every client shares). Putting the path here now means the component DTOs can join it in this
/// namespace when the picker needs them, instead of the Web project growing a reference it should
/// not have.
/// </para>
/// <para>
/// The values map one-to-one onto <c>ComponentPlatform.Intel</c> / <c>Amd</c> in the domain;
/// <see cref="Ready"/> has no platform of its own because it lists pre-built assemblies from both.
/// </para>
/// </summary>
public enum ConfiguratorPath
{
    Intel = 0,
    Amd = 1,
    Ready = 2
}

/// <summary>
/// Maps <see cref="ConfiguratorPath"/> to and from the <c>?build=</c> query token.
/// <para>
/// The tokens are part of the site's URL surface — the Phase 13 home page already links to
/// <c>/configurator?build=intel</c> and <c>?build=amd</c> — so they are fixed strings here rather
/// than <c>Enum.ToString()</c>, which would silently change the URLs if a member were ever renamed.
/// </para>
/// </summary>
public static class ConfiguratorPaths
{
    public const string QueryKey = "build";

    private const string IntelToken = "intel";
    private const string AmdToken = "amd";
    private const string ReadyToken = "ready";

    /// <summary>Query token for <paramref name="path"/>.</summary>
    public static string ToToken(ConfiguratorPath path) => path switch
    {
        ConfiguratorPath.Intel => IntelToken,
        ConfiguratorPath.Amd => AmdToken,
        ConfiguratorPath.Ready => ReadyToken,
        _ => IntelToken
    };

    /// <summary>
    /// Parses a <c>?build=</c> token, or <see langword="null"/> when it is absent or unrecognised —
    /// which is what makes an unknown value fall back to the chooser rather than erroring.
    /// </summary>
    public static ConfiguratorPath? Parse(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        IntelToken => ConfiguratorPath.Intel,
        AmdToken => ConfiguratorPath.Amd,
        ReadyToken => ConfiguratorPath.Ready,
        _ => null
    };
}
