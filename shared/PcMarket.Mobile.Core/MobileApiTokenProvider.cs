using PcMarket.ApiClient;

namespace PcMarket.Mobile.Core;

/// <summary>Feeds the API clients the current session's access and guest-cart tokens. Because it is consulted
/// on every request, it is also where the stored session is guaranteed to be hydrated — that keeps the
/// keystore read off the app's start-up path without any request ever going out unauthenticated.</summary>
public sealed class MobileApiTokenProvider(MobileSession session) : IApiTokenProvider
{
    public async ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await session.LoadAsync(cancellationToken);
        return session.AccessToken;
    }

    public async ValueTask<string?> GetCartTokenAsync(CancellationToken cancellationToken = default)
    {
        await session.LoadAsync(cancellationToken);
        return session.CartToken;
    }
}
