using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace PcMarket.Web.Services;

/// <summary>Persists the <see cref="WebSession"/> to encrypted, per-session browser storage so a page reload
/// keeps the shopper signed in and their guest cart attached. Storage access needs JS interop, so calls made
/// before the circuit is interactive are swallowed and simply leave the session empty.</summary>
public sealed class SessionStore(ProtectedSessionStorage storage)
{
    private const string Key = "pcmarket.session";

    public async Task LoadIntoAsync(WebSession session)
    {
        try
        {
            var result = await storage.GetAsync<PersistedSession>(Key);
            if (result.Success && result.Value is not null)
            {
                session.Apply(result.Value);
            }
        }
        catch
        {
            // JS interop not available yet (pre-interactive) — treat as an empty session.
        }

        session.Loaded = true;
    }

    public async Task SaveAsync(WebSession session)
    {
        try
        {
            await storage.SetAsync(Key, session.Snapshot());
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    public async Task ClearAsync(WebSession session)
    {
        session.SignOut();
        try
        {
            await storage.DeleteAsync(Key);
        }
        catch
        {
            // Best-effort.
        }
    }
}
