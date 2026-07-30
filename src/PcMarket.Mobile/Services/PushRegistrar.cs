using PcMarket.ApiClient;
using PcMarket.Contracts.Users;
using PcMarket.Mobile.Core;
// MAUI has its own DevicePlatform in scope via implicit usings; be explicit about the wire enum.
using ClientPlatform = PcMarket.Contracts.Users.DevicePlatform;

namespace PcMarket.Mobile.Services;

/// <summary>Supplies this device's push registration token. Implemented per platform; a build without
/// Firebase configuration returns null, which the registrar treats as "push unavailable".</summary>
public interface IPushTokenSource
{
    /// <summary>The FCM registration token, or null when push is not available on this build/device.</summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    ClientPlatform Platform { get; }
}

/// <summary>Keeps the backend's device registry in step with this install: registers the token once the user
/// is signed in, and drops it on sign-out so a shared device stops receiving the previous user's orders.
/// Every failure here is swallowed — push is an enhancement, and losing it must never block signing in.</summary>
public sealed class PushRegistrar(
    IPushTokenSource tokenSource,
    UsersApiClient users,
    MobileSession session,
    SessionGuard guard)
{
    private string? _registeredToken;

    public async Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated)
        {
            return;
        }

        try
        {
            var token = await tokenSource.GetTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("[push] no registration token; push notifications are unavailable.");
                return;
            }

            await guard.ExecuteAsync(
                ct => users.RegisterDeviceTokenAsync(new RegisterDeviceTokenRequest(token, tokenSource.Platform), ct),
                cancellationToken);

            _registeredToken = token;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[push] registration failed: {ex.Message}");
        }
    }

    public async Task UnregisterAsync(CancellationToken cancellationToken = default)
    {
        if (_registeredToken is null || !session.IsAuthenticated)
        {
            return;
        }

        try
        {
            await guard.ExecuteAsync(ct => users.DeleteDeviceTokenAsync(_registeredToken, ct), cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[push] unregistration failed: {ex.Message}");
        }
        finally
        {
            _registeredToken = null;
        }
    }
}
