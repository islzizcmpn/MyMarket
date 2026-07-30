namespace PcMarket.ApiClient;

/// <summary>Supplies the per-request auth and guest-cart tokens. The consuming app (web/mobile/bot) implements
/// this against its own session store; the client stays transport-only.</summary>
public interface IApiTokenProvider
{
    ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    ValueTask<string?> GetCartTokenAsync(CancellationToken cancellationToken = default);
}
