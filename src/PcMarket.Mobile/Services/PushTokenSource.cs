using ClientPlatform = PcMarket.Contracts.Users.DevicePlatform;

namespace PcMarket.Mobile.Services;

/// <summary>Reads this device's push registration token.
///
/// It currently returns null on every platform, which <see cref="PushRegistrar"/> reports as "push
/// unavailable" and carries on — the app is fully usable without it. The Firebase Messaging binding it
/// would use cannot be referenced yet (see the note in PcMarket.Mobile.csproj: its AndroidX constraints
/// conflict with .NET 10 MAUI's), and no Firebase project exists to verify against. Everything downstream —
/// registration, the API endpoint, the device registry, and the push notification channel — is real and
/// exercised by tests, so enabling push is a change to this one method plus a google-services.json.
/// See docs/specs/pcmarket_clone/mobile_app/push-setup.md.</summary>
public sealed class PushTokenSource : IPushTokenSource
{
    public ClientPlatform Platform =>
        DeviceInfo.Platform == DevicePlatform.iOS ? ClientPlatform.Ios : ClientPlatform.Android;

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine(
            "[push] no messaging SDK configured on this build; device will not be registered for push.");

        return Task.FromResult<string?>(null);
    }
}
