namespace PcMarket.Mobile.Core;

/// <summary>Key/value persistence for the mobile session, split by sensitivity: credentials go to the
/// platform keystore, everything else to ordinary preferences. Abstracted so the session logic is testable
/// off-device — MAUI's <c>SecureStorage</c> and <c>Preferences</c> are static platform APIs.</summary>
public interface ISessionStorage
{
    /// <summary>Reads from the platform keystore. Returns null when absent or unreadable.</summary>
    Task<string?> GetSecureAsync(string key, CancellationToken cancellationToken = default);

    Task SetSecureAsync(string key, string value, CancellationToken cancellationToken = default);

    Task RemoveSecureAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Reads a non-sensitive value (the guest cart token).</summary>
    string? GetPlain(string key);

    /// <summary>Writes a non-sensitive value; a null value removes the key.</summary>
    void SetPlain(string key, string? value);
}
