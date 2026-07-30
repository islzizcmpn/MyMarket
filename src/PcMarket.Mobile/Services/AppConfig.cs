namespace PcMarket.Mobile.Services;

/// <summary>Where the app points at the backend. The Android emulator reaches the host machine at
/// 10.0.2.2 (its own <c>localhost</c> is the emulated device), which is why a debug build cannot simply
/// use <c>localhost</c>; see also the cleartext-traffic exception in the Android manifest.</summary>
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
}
