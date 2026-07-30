using PcMarket.ApiClient;

namespace PcMarket.Web.Services;

/// <summary>Feeds the API client the current circuit's access and guest-cart tokens.</summary>
public sealed class WebApiTokenProvider(WebSession session) : IApiTokenProvider
{
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(session.AccessToken);

    public ValueTask<string?> GetCartTokenAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(session.CartToken);
}
