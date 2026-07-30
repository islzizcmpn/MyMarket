using PcMarket.Mobile.Core;

namespace PcMarket.UnitTests.Mobile;

/// <summary>Stands in for the device keystore and preferences. Survives being handed to a second
/// <see cref="MobileSession"/>, which is how "the user is still signed in after a restart" is tested.</summary>
public sealed class InMemorySessionStorage : ISessionStorage
{
    private readonly Dictionary<string, string> _secure = [];
    private readonly Dictionary<string, string> _plain = [];

    /// <summary>Set to simulate a keystore whose keys were invalidated (OS upgrade, restore from backup).</summary>
    public bool FailSecureReads { get; set; }

    public int SecureWrites { get; private set; }

    public Task<string?> GetSecureAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(FailSecureReads ? null : _secure.GetValueOrDefault(key));

    public Task SetSecureAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _secure[key] = value;
        SecureWrites++;
        return Task.CompletedTask;
    }

    public Task RemoveSecureAsync(string key, CancellationToken cancellationToken = default)
    {
        _secure.Remove(key);
        return Task.CompletedTask;
    }

    public string? GetPlain(string key) => _plain.GetValueOrDefault(key);

    public void SetPlain(string key, string? value)
    {
        if (value is null)
        {
            _plain.Remove(key);
        }
        else
        {
            _plain[key] = value;
        }
    }

    /// <summary>Overwrites the stored session with something unparsable.</summary>
    public void CorruptSession() => _secure["pcmarket.session"] = "{ not json";

    public bool HasStoredSession => _secure.ContainsKey("pcmarket.session");
}
