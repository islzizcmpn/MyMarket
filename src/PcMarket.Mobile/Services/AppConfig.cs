namespace PcMarket.Mobile.Services;

/// <summary>Where the app points at the backend and at the storefront's artwork. The Android emulator
/// reaches the host machine at 10.0.2.2 (its own <c>localhost</c> is the emulated device), which is why a
/// debug build cannot simply use <c>localhost</c>; see also the cleartext-traffic exception in the Android
/// manifest, which covers both hosts and both ports.</summary>
public static class AppConfig
{
    public static string ApiRootUrl =>
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5055"
            : "http://localhost:5055";
#else
        "https://api.pcmarket.uz";
#endif

    /// <summary>Root for the storefront's decorative artwork, which is fetched at runtime rather than
    /// bundled — the photography under <c>PcMarket.Web/wwwroot/images</c> is an order of magnitude larger
    /// than the whole app package. A physical device reaches a locally running storefront through
    /// <c>adb reverse tcp:5193 tcp:5193</c>; an emulator uses the same host alias as the API.</summary>
    public static string MediaRootUrl =>
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5193"
            : "http://localhost:5193";
#else
        "https://pcmarket.uz";
#endif
}
