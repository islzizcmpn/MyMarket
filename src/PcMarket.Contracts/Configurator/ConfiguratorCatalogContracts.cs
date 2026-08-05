namespace PcMarket.Contracts.Configurator;

/// <summary>
/// Wire mirrors of the configurator's domain enums (Phase 19). Declared here rather than shared with
/// <c>PcMarket.Domain.Configurator</c> because Contracts deliberately has no project references —
/// it is the one assembly every client can take. <b>Values must stay in step with the domain
/// enums</b>, which is what lets the API project across with a plain cast.
/// </summary>
public enum ConfiguratorCategory
{
    Motherboard = 0,
    Cpu = 1,
    Cooler = 2,
    Ram = 3,
    Storage = 4,
    Gpu = 5,
    Psu = 6,
    Case = 7
}

/// <summary>Which build a part belongs to. Distinct from <see cref="ConfiguratorPath"/>: a part can
/// be vendor-neutral (<see cref="Either"/>), but an entry path never is.</summary>
public enum ConfiguratorPlatform
{
    Either = 0,
    Intel = 1,
    Amd = 2
}

public enum ConfiguratorRamType
{
    Ddr4 = 0,
    Ddr5 = 1
}

public enum ConfiguratorFormFactor
{
    Itx = 0,
    MicroAtx = 1,
    Atx = 2
}

/// <summary>
/// One selectable part, as the storefront sees it.
/// <para>
/// The spec fields are carried even though the stage-3 picker only renders name, price and image:
/// the compatibility rules run client-side over the selection in stage 4, and shipping the specs now
/// means that stage changes a page rather than the API contract.
/// </para>
/// </summary>
public sealed record ConfiguratorComponentDto(
    string Id,
    string Name,
    ConfiguratorCategory Category,
    decimal Price,
    string? ImageUrl,
    ConfiguratorPlatform Platform,
    string? Socket,
    int? PowerDraw,
    ConfiguratorRamType? RamType,
    ConfiguratorFormFactor? FormFactor,
    int? LengthMm,
    int? Wattage,
    int? HeightMm,
    IReadOnlyList<ConfiguratorFormFactor>? SupportedFormFactors,
    int? MaxGpuLengthMm,
    int? MaxCoolerHeightMm,
    IReadOnlyList<string>? SocketSupport);

/// <summary>A pre-built assembly behind the "Ready-made" path, with its parts already resolved.</summary>
public sealed record ConfiguratorBundleDto(
    string Id,
    string Name,
    string Description,
    ConfiguratorPlatform Platform,
    IReadOnlyList<string> ComponentIds,
    decimal TotalPrice);

/// <summary>
/// The whole configurator catalog in one response. Deliberately a single payload rather than a call
/// per category: it is a few dozen fixed rows, and the picker lets a shopper move between categories
/// freely — paying a round trip per click would make the fastest interaction on the page the
/// slowest.
/// </summary>
public sealed record ConfiguratorCatalogDto(
    IReadOnlyList<ConfiguratorComponentDto> Components,
    IReadOnlyList<ConfiguratorBundleDto> Bundles);
