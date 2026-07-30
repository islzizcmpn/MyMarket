using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;
using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.Services;

/// <summary>Everything that must happen around a sign-in or sign-out, in one place so the login, OTP, and
/// account screens cannot each get it subtly different: persist the session, fold in the guest cart, and
/// keep the push registry current.</summary>
public sealed class AuthFlow(
    MobileSession session,
    StoreCart cart,
    AuthApiClient auth,
    PushRegistrar push)
{
    public async Task CompleteSignInAsync(AuthResponse response, CancellationToken cancellationToken = default)
    {
        await session.SetAuthAsync(response, cancellationToken);
        await cart.MergeGuestCartAsync(cancellationToken);
        await push.RegisterAsync(cancellationToken);
    }

    /// <summary>Signs out locally no matter what the server says — a revoke that fails (offline, already
    /// expired) must still clear this device.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await push.UnregisterAsync(cancellationToken);

        var refreshToken = session.RefreshToken;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await auth.LogoutAsync(new LogoutRequest(refreshToken), cancellationToken);
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException)
            {
                System.Diagnostics.Debug.WriteLine($"[auth] refresh-token revoke failed: {ex.Message}");
            }
        }

        await session.SignOutAsync(cancellationToken);
        cart.Clear();
    }
}
