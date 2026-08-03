using System.Globalization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Localization;

namespace PcMarket.Admin.Services;

/// <summary>Persists the admin session to encrypted per-session browser storage.</summary>
public sealed class SessionStore(ProtectedSessionStorage storage)
{
    private const string Key = "pcmarket.admin.session";

    public async Task LoadIntoAsync(AdminSession session)
    {
        try
        {
            var result = await storage.GetAsync<PersistedAdminSession>(Key);
            if (result.Success && result.Value is not null)
            {
                session.Apply(result.Value);
            }
        }
        catch { /* pre-interactive; leave empty */ }
        session.Loaded = true;
    }

    public async Task SaveAsync(AdminSession session)
    {
        try { await storage.SetAsync(Key, session.Snapshot()); } catch { }
    }

    public async Task ClearAsync(AdminSession session)
    {
        session.SignOut();
        try { await storage.DeleteAsync(Key); } catch { }
    }
}

/// <summary>Money presentation for the admin UI. Digits are grouped by spaces the way the storefront writes
/// them; the currency word comes from the panel's own string table, so it follows the chosen language.</summary>
public static class Format
{
    private static readonly CultureInfo Grouped = Build();

    public static string Money(IStringLocalizer localizer, decimal amount) =>
        $"{amount.ToString("#,0", Grouped)} {localizer["Common.Currency"]}";

    private static CultureInfo Build()
    {
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.NumberGroupSeparator = " ";
        return c;
    }
}
