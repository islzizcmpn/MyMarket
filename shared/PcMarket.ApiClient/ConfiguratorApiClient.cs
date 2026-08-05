using PcMarket.Contracts.Configurator;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the PC configurator's parts catalog (Phase 19).</summary>
public sealed class ConfiguratorApiClient(HttpClient http, IApiTokenProvider tokens)
    : ApiClientBase(http, tokens)
{
    /// <summary>Every selectable part and every ready-made bundle, in one call.</summary>
    public Task<ConfiguratorCatalogDto> GetCatalogAsync(CancellationToken ct = default) =>
        GetAsync<ConfiguratorCatalogDto>("configurator/catalog", ct);
}
