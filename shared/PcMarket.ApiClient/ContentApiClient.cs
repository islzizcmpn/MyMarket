using PcMarket.Contracts.Content;

namespace PcMarket.ApiClient;

/// <summary>Typed access to published storefront content (banners, CMS blocks).</summary>
public sealed class ContentApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<IReadOnlyList<BannerDto>> GetBannersAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<BannerDto>>("content/banners", ct);

    public Task<CmsBlockDto?> GetBlockAsync(string key, CancellationToken ct = default) =>
        GetOrDefaultAsync<CmsBlockDto>($"content/blocks/{Uri.EscapeDataString(key)}", ct);
}
