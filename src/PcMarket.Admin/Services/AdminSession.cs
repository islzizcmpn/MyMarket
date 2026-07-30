using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;

namespace PcMarket.Admin.Services;

public sealed record PersistedAdminSession(string? AccessToken, string? RefreshToken, Guid? UserId, IReadOnlyList<string> Roles);

/// <summary>Per-circuit auth state for the admin panel, mirrored to protected browser storage.</summary>
public sealed class AdminSession
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public Guid? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public bool Loaded { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && UserId is not null;

    public event Action? Changed;

    public void SetAuth(AuthResponse response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        UserId = response.UserId;
        Roles = response.Roles;
        Changed?.Invoke();
    }

    public void Apply(PersistedAdminSession p)
    {
        AccessToken = p.AccessToken;
        RefreshToken = p.RefreshToken;
        UserId = p.UserId;
        Roles = p.Roles;
        Changed?.Invoke();
    }

    public PersistedAdminSession Snapshot() => new(AccessToken, RefreshToken, UserId, Roles);

    public void SignOut()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        Roles = [];
        Changed?.Invoke();
    }
}

/// <summary>Feeds the API client the admin's access token (admin has no guest-cart token).</summary>
public sealed class AdminApiTokenProvider(AdminSession session) : IApiTokenProvider
{
    public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) => ValueTask.FromResult(session.AccessToken);
    public ValueTask<string?> GetCartTokenAsync(CancellationToken ct = default) => ValueTask.FromResult<string?>(null);
}
