using Android.Views;
using Microsoft.Maui.Platform;

namespace PcMarket.Mobile.Services;

internal static partial class SystemBars
{
    // WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS / APPEARANCE_LIGHT_NAVIGATION_BARS.
    private const int AppearanceLightStatusBars = 8;
    private const int AppearanceLightNavigationBars = 16;

    static partial void ApplyPlatform(Color background, bool darkTheme)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window is not { } window)
        {
            // Called before the activity is up, or after it has gone. The next resume repaints.
            return;
        }

        var fill = background.ToPlatform();

        activity.RunOnUiThread(() =>
        {
            // Deprecated from API 35, where the platform draws the bars transparent and ignores
            // these. The app supports API 21 up and its target device is API 30, so this is still
            // the only way to colour the bars there; on 35+ the calls are simply inert.
#pragma warning disable CA1422
            window.SetStatusBarColor(fill);
            window.SetNavigationBarColor(fill);
#pragma warning restore CA1422

            ApplyIconContrast(window, darkTheme);
        });
    }

    /// <summary>Dark chrome takes light icons and light chrome takes dark ones, which is the
    /// inverse of the flags: the "light bars" flags mean the bar background is light.</summary>
    private static void ApplyIconContrast(Android.Views.Window window, bool darkTheme)
    {
        const int LightBars = AppearanceLightStatusBars | AppearanceLightNavigationBars;

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.InsetsController?.SetSystemBarsAppearance(darkTheme ? 0 : LightBars, LightBars);
            return;
        }

        // The pre-API-30 equivalent, still needed because the app supports API 21 up. The two
        // flags arrived in different releases, so each is gated separately; below API 23 the
        // platform has no dark-icon mode at all and the bars keep their light icons.
#pragma warning disable CA1422
        var decor = window.DecorView;
        var flags = decor.SystemUiFlags;

        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags = darkTheme
                ? flags & ~SystemUiFlags.LightStatusBar
                : flags | SystemUiFlags.LightStatusBar;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            flags = darkTheme
                ? flags & ~SystemUiFlags.LightNavigationBar
                : flags | SystemUiFlags.LightNavigationBar;
        }

        decor.SystemUiFlags = flags;
#pragma warning restore CA1422
    }
}
