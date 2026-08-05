namespace PcMarket.Domain.Configurator;

/// <summary>
/// One selectable part in the Phase 19 configurator.
/// <para>
/// Deliberately one flat type rather than a per-category hierarchy: the compatibility rules read
/// across categories (a case constrains the GPU and the cooler; a motherboard constrains the CPU and
/// the RAM), and a hierarchy would have every rule downcasting before it could do its job. The cost
/// is that every spec below is optional — a PSU has no socket, a case has no power draw — so each
/// rule is responsible for checking that the fields it needs are actually present, and skipping
/// quietly when they are not.
/// </para>
/// <para>
/// This is reference data, not a persisted entity: it carries no key generation, no audit fields and
/// no domain events, and it is seeded from <see cref="ComponentCatalog"/> the same way
/// <c>UzbekistanRegions</c> seeds its list. If configurator parts ever become admin-managed, this
/// record is what a `ConfiguratorComponent` entity would be projected into.
/// </para>
/// </summary>
public sealed record Component
{
    /// <summary>Stable identifier, unique across the whole catalog. Used by selection state and by
    /// the shareable build link, so it must not change once published.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ComponentCategory Category { get; init; }

    /// <summary>Price in the store's base currency (UZS), matching the catalog's <c>decimal(18,2)</c>.</summary>
    public required decimal Price { get; init; }

    /// <summary>Site-relative image path, or <see langword="null"/> for parts with no artwork yet —
    /// the UI falls back rather than emitting a broken image.</summary>
    public string? ImageUrl { get; init; }

    public ComponentPlatform Platform { get; init; } = ComponentPlatform.Either;

    // ---- Specs the compatibility rules read. Each is populated only for the categories that have it.

    /// <summary>CPU socket (<see cref="ComponentCategory.Cpu"/>) or the socket a board accepts
    /// (<see cref="ComponentCategory.Motherboard"/>). See <see cref="Sockets"/>.</summary>
    public string? Socket { get; init; }

    /// <summary>Sustained draw in watts. Set on CPUs and GPUs, which is where essentially all of a
    /// build's power budget goes; <see langword="null"/> elsewhere and treated as zero.</summary>
    public int? PowerDraw { get; init; }

    /// <summary>Memory generation. Set on RAM and on the motherboard that accepts it.</summary>
    public RamType? RamType { get; init; }

    /// <summary>Board size. Set on motherboards; a case declares what it fits in
    /// <see cref="SupportedFormFactors"/> instead.</summary>
    public FormFactor? FormFactor { get; init; }

    /// <summary>Card length in mm (<see cref="ComponentCategory.Gpu"/>).</summary>
    public int? LengthMm { get; init; }

    /// <summary>Rated output in watts (<see cref="ComponentCategory.Psu"/>).</summary>
    public int? Wattage { get; init; }

    /// <summary>Cooler height in mm (<see cref="ComponentCategory.Cooler"/>). For an AIO this is the
    /// pump/block height, not the radiator, since that is what the side panel has to clear.</summary>
    public int? HeightMm { get; init; }

    /// <summary>Board sizes a case has standoffs for (<see cref="ComponentCategory.Case"/>).</summary>
    public IReadOnlyList<FormFactor>? SupportedFormFactors { get; init; }

    /// <summary>Maximum card length a case accepts, in mm (<see cref="ComponentCategory.Case"/>).</summary>
    public int? MaxGpuLengthMm { get; init; }

    /// <summary>Maximum cooler height a case accepts, in mm (<see cref="ComponentCategory.Case"/>).</summary>
    public int? MaxCoolerHeightMm { get; init; }

    /// <summary>Sockets a cooler ships mounting hardware for (<see cref="ComponentCategory.Cooler"/>).</summary>
    public IReadOnlyList<string>? SocketSupport { get; init; }
}
