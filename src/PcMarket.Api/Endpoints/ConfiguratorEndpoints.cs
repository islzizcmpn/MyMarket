using PcMarket.Contracts.Configurator;
using PcMarket.Domain.Configurator;

namespace PcMarket.Api.Endpoints;

/// <summary>
/// Public read access to the Phase 19 configurator catalog.
/// <para>
/// This endpoint exists because <c>ComponentCatalog</c> lives in <c>PcMarket.Domain</c>, which the
/// storefront is not allowed to reference — the architecture keeps Domain backend-only and makes
/// Contracts the single DTO source every client shares. Serving the parts here is what lets the Web
/// picker consume them the same way it consumes the product catalog, instead of the Web project
/// growing a Domain reference.
/// </para>
/// <para>
/// The data is static for the life of the process, so the response is output-cached rather than
/// recomputed per request.
/// </para>
/// </summary>
public static class ConfiguratorEndpoints
{
    public static void MapConfiguratorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/configurator").WithTags("Configurator");

        group.MapGet("/catalog", () => new ConfiguratorCatalogDto(
                [.. ComponentCatalog.All.Select(ToDto)],
                [.. ComponentCatalog.Bundles.Select(ToDto)]))
            .CacheOutput()
            .WithName("GetConfiguratorCatalog");
    }

    private static ConfiguratorComponentDto ToDto(Component part) => new(
        part.Id,
        part.Name,
        (ConfiguratorCategory)part.Category,
        part.Price,
        part.ImageUrl,
        (ConfiguratorPlatform)part.Platform,
        part.Socket,
        part.PowerDraw,
        (ConfiguratorRamType?)part.RamType,
        (ConfiguratorFormFactor?)part.FormFactor,
        part.LengthMm,
        part.Wattage,
        part.HeightMm,
        part.SupportedFormFactors?.Select(size => (ConfiguratorFormFactor)size).ToList(),
        part.MaxGpuLengthMm,
        part.MaxCoolerHeightMm,
        part.SocketSupport);

    // The total is computed here rather than left to each client, so every surface that shows a
    // ready-made build's price agrees on it.
    private static ConfiguratorBundleDto ToDto(AssemblyBundle bundle) => new(
        bundle.Id,
        bundle.Name,
        bundle.Description,
        (ConfiguratorPlatform)bundle.Platform,
        bundle.ComponentIds,
        ComponentCatalog.TotalPrice(ComponentCatalog.Resolve(bundle)));
}
