using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.Services;

/// <summary>Binds the session store to the platform: credentials to the keystore (Android Keystore /
/// iOS Keychain), the guest cart token to ordinary preferences. Keystore reads can throw on a device whose
/// keys were invalidated (OS upgrade, backup restore); that is treated as "no session" rather than a crash.</summary>
public sealed class MauiSessionStorage : ISessionStorage
{
    public async Task<string?> GetSecureAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[session] secure read failed: {ex.Message}");
            SecureStorage.Default.Remove(key);
            return null;
        }
    }

    public async Task SetSecureAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            // Losing persistence only costs the user a re-login next launch; it must not fail the sign-in.
            System.Diagnostics.Debug.WriteLine($"[session] secure write failed: {ex.Message}");
        }
    }

    public Task RemoveSecureAsync(string key, CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    public string? GetPlain(string key) => Preferences.Default.Get<string?>(key, null);

    public void SetPlain(string key, string? value)
    {
        if (value is null)
        {
            Preferences.Default.Remove(key);
        }
        else
        {
            Preferences.Default.Set(key, value);
        }
    }
}
