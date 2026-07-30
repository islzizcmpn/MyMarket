using System.Net;
using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;

namespace PcMarket.Mobile.Core;

/// <summary>Keeps the access token usable around every authenticated call: refreshes it proactively once it
/// has expired, and retries once if the server rejects it anyway. Refreshes are serialised and keyed on the
/// token that failed, so several screens loading at once rotate the refresh token a single time instead of
/// racing each other into revoking one another's.</summary>
public sealed class SessionGuard(MobileSession session, AuthApiClient auth, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Raised when the session could not be renewed and the user was signed out.</summary>
    public event Action? SignedOut;

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (session.NeedsRefresh(_time.GetUtcNow()))
        {
            await TryRefreshAsync(session.AccessToken, cancellationToken);
        }

        var tokenInUse = session.AccessToken;

        try
        {
            return await action(cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized && session.IsAuthenticated)
        {
            if (!await TryRefreshAsync(tokenInUse, cancellationToken))
            {
                throw;
            }

            return await action(cancellationToken);
        }
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);

    /// <param name="staleToken">The access token the caller found unusable. If the session has since moved on
    /// to a different token, another caller already refreshed and there is nothing to do.</param>
    private async Task<bool> TryRefreshAsync(string? staleToken, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!session.IsAuthenticated)
            {
                return false;
            }

            if (session.AccessToken != staleToken)
            {
                return true;
            }

            var refreshToken = session.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                await SignOutAsync(cancellationToken);
                return false;
            }

            try
            {
                var renewed = await auth.RefreshAsync(new RefreshRequest(refreshToken), cancellationToken);
                await session.SetAuthAsync(renewed, cancellationToken);
                return true;
            }
            catch (ApiException)
            {
                // Revoked, rotated away, or expired: nothing to recover from, so sign out rather than loop.
                await SignOutAsync(cancellationToken);
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await session.SignOutAsync(cancellationToken);
        SignedOut?.Invoke();
    }
}
