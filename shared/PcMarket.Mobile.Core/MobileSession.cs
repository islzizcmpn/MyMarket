using System.Text.Json;
using PcMarket.Contracts.Auth;

namespace PcMarket.Mobile.Core;

/// <summary>Serializable snapshot of the authenticated session, stored in the platform keystore.</summary>
public sealed record PersistedSession(
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid? UserId,
    IReadOnlyList<string> Roles);

/// <summary>App-wide auth + guest-cart state. Mirrors the storefront's <c>WebSession</c>, but scoped to the
/// process (one user per install) and persisted across launches: credentials in the keystore, the guest cart
/// token in preferences. Raises <see cref="Changed"/> so tabs and badges can react.</summary>
public sealed class MobileSession(ISessionStorage storage)
{
    internal const string SessionKey = "pcmarket.session";
    internal const string CartTokenKey = "pcmarket.cart-token";

    /// <summary>How long before actual expiry an access token is treated as stale, covering request latency
    /// and small clock differences between device and server.</summary>
    public static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpiresAt { get; private set; }
    public string? CartToken { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>True once the session has been hydrated from storage this launch.</summary>
    public bool Loaded { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && UserId is not null;

    public event Action? Changed;

    /// <summary>True when there is a token that has expired (or is about to) and should be refreshed.</summary>
    public bool NeedsRefresh(DateTimeOffset now) =>
        IsAuthenticated && AccessTokenExpiresAt - ExpiryLeeway <= now;

    /// <summary>Restores the previous session. Idempotent and safe to call from several places at once —
    /// the token provider calls it before every request, so hydration never has to block app start-up.
    /// Corrupt or unreadable storage is treated as "signed out" rather than a crash.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Loaded)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (Loaded)
            {
                return;
            }

            await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        CartToken = storage.GetPlain(CartTokenKey);

        var raw = await storage.GetSecureAsync(SessionKey, cancellationToken);
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<PersistedSession>(raw);
                if (persisted is not null)
                {
                    AccessToken = persisted.AccessToken;
                    RefreshToken = persisted.RefreshToken;
                    AccessTokenExpiresAt = persisted.AccessTokenExpiresAt;
                    UserId = persisted.UserId;
                    Roles = persisted.Roles;
                }
            }
            catch (JsonException)
            {
                await storage.RemoveSecureAsync(SessionKey, cancellationToken);
            }
        }

        Loaded = true;
        Changed?.Invoke();
    }

    public async Task SetAuthAsync(AuthResponse response, CancellationToken cancellationToken = default)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAt = response.AccessTokenExpiresAt;
        UserId = response.UserId;
        Roles = response.Roles;

        await PersistAsync(cancellationToken);
        Changed?.Invoke();
    }

    public async Task SetCartTokenAsync(string? cartToken, CancellationToken cancellationToken = default)
    {
        if (CartToken == cartToken)
        {
            return;
        }

        CartToken = cartToken;
        storage.SetPlain(CartTokenKey, cartToken);
        await Task.CompletedTask;
        Changed?.Invoke();
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = default;
        UserId = null;
        Roles = [];

        await storage.RemoveSecureAsync(SessionKey, cancellationToken);
        Changed?.Invoke();
    }

    public PersistedSession Snapshot() => new(AccessToken, RefreshToken, AccessTokenExpiresAt, UserId, Roles);

    private Task PersistAsync(CancellationToken cancellationToken) =>
        storage.SetSecureAsync(SessionKey, JsonSerializer.Serialize(Snapshot()), cancellationToken);
}
