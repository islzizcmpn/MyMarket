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

        // Not cached: the answer depends entirely on the posted selection.
        group.MapPost("/evaluate", (ConfiguratorEvaluateRequest request) => Evaluate(request))
            .WithName("EvaluateConfiguratorBuild");
    }

    /// <summary>
    /// Runs the domain's <see cref="CompatibilityChecker"/> over a posted selection.
    /// <para>
    /// This is a server call rather than storefront logic on purpose. The rules are covered by the
    /// stage-1 unit tests against <c>PcMarket.Domain</c>, and the storefront cannot reference Domain
    /// — so evaluating here keeps exactly one implementation of the rules instead of a tested copy
    /// and a drifting client copy.
    /// </para>
    /// <para>
    /// The per-part preview is folded into the same response for the same reason, and to keep it to
    /// one round trip: marking a category's options means re-running the rules once per candidate,
    /// which would otherwise be a request each.
    /// </para>
    /// </summary>
    private static ConfiguratorEvaluationDto Evaluate(ConfiguratorEvaluateRequest request)
    {
        var selected = (request.SelectedIds ?? [])
            .Select(ComponentCatalog.Find)
            .OfType<Component>()
            .ToList();

        List<ComponentCompatibilityDto> previews = [];

        if (request.PreviewCategory is { } previewCategory)
        {
            var category = (ComponentCategory)previewCategory;

            // The rest of the build, with this category's current pick taken out — the candidate is
            // what fills the hole. Without removing it, every candidate would be judged against the
            // part it is meant to replace.
            var others = selected.Where(part => part.Category != category).ToList();

            previews =
            [
                .. ComponentCatalog.InCategory(category).Select(candidate =>
                    new ComponentCompatibilityDto(
                        candidate.Id,
                        [
                            // Only the problems this candidate is party to. A build that already has
                            // an unrelated conflict should not paint every option in the open
                            // category as incompatible.
                            .. CompatibilityChecker.Check([.. others, candidate])
                                .Where(warning => warning.ComponentIds.Contains(candidate.Id))
                                .Select(warning => (CompatibilityIssueDto)warning.Issue)
                        ]))
            ];
        }

        return new ConfiguratorEvaluationDto(
            [.. CompatibilityChecker.Check(selected).Select(ToDto)],
            previews,
            CompatibilityChecker.TotalPowerDraw(selected),
            CompatibilityChecker.RequiredWattage(selected));
    }

    private static CompatibilityWarningDto ToDto(CompatibilityWarning warning) => new(
        (CompatibilityIssueDto)warning.Issue,
        warning.Message,
        warning.ComponentIds);

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
