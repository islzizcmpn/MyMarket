using PcMarket.Contracts.Configurator;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the PC configurator's parts catalog (Phase 19).</summary>
public sealed class ConfiguratorApiClient(HttpClient http, IApiTokenProvider tokens)
    : ApiClientBase(http, tokens)
{
    /// <summary>Every selectable part and every ready-made bundle, in one call.</summary>
    public Task<ConfiguratorCatalogDto> GetCatalogAsync(CancellationToken ct = default) =>
        GetAsync<ConfiguratorCatalogDto>("configurator/catalog", ct);

    /// <summary>
    /// Checks a build against the compatibility rules, and — when
    /// <see cref="ConfiguratorEvaluateRequest.PreviewCategory"/> is set — reports which parts in
    /// that category would conflict if chosen.
    /// </summary>
    public Task<ConfiguratorEvaluationDto> EvaluateAsync(
        ConfiguratorEvaluateRequest request, CancellationToken ct = default) =>
        PostAsync<ConfiguratorEvaluateRequest, ConfiguratorEvaluationDto>("configurator/evaluate", request, ct);
}
