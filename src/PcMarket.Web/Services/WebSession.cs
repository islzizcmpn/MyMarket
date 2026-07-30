using PcMarket.Contracts.Auth;

namespace PcMarket.Web.Services;

/// <summary>Serializable snapshot of the session persisted to protected browser storage.</summary>
public sealed record PersistedSession(
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string? CartToken,
    Guid? UserId,
    IReadOnlyList<string> Roles);

/// <summary>Per-circuit auth + guest-cart state for the storefront, held in memory and mirrored to protected
/// browser storage (see <see cref="SessionStore"/>). Raises <see cref="Changed"/> so the header can react.</summary>
public sealed class WebSession
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpiresAt { get; private set; }
    public string? CartToken { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>True once the session has been hydrated from browser storage this circuit.</summary>
    public bool Loaded { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && UserId is not null;

    public event Action? Changed;

    public void SetAuth(AuthResponse response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        AccessTokenExpiresAt = response.AccessTokenExpiresAt;
        UserId = response.UserId;
        Roles = response.Roles;
        Changed?.Invoke();
    }

    public void SetCartToken(string? cartToken)
    {
        if (CartToken == cartToken)
        {
            return;
        }

        CartToken = cartToken;
        Changed?.Invoke();
    }

    public void Apply(PersistedSession persisted)
    {
        AccessToken = persisted.AccessToken;
        RefreshToken = persisted.RefreshToken;
        AccessTokenExpiresAt = persisted.AccessTokenExpiresAt;
        CartToken = persisted.CartToken;
        UserId = persisted.UserId;
        Roles = persisted.Roles;
        Changed?.Invoke();
    }

    public PersistedSession Snapshot() =>
        new(AccessToken, RefreshToken, AccessTokenExpiresAt, CartToken, UserId, Roles);

    public void SignOut()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = default;
        UserId = null;
        Roles = [];
        Changed?.Invoke();
    }
}
