using PcMarket.Mobile.Services;

namespace PcMarket.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ThemeService _theme;

    public App(IServiceProvider services, ThemeService theme)
    {
        // Ahead of InitializeComponent so UserAppTheme is already settled when the merged
        // dictionaries load: every AppThemeBinding in them resolves against the stored choice on
        // the first paint rather than resolving dark and then flipping.
        theme.Restore();

        // Loads Application.Resources (colours, brushes, styles, converters) — every page's
        // StaticResource lookups resolve against them, so this must run before the first page is built.
        InitializeComponent();
        _services = services;
        _theme = theme;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // The stored session is hydrated lazily by the API token provider, so start-up never blocks on a
        // keystore read; see MobileApiTokenProvider.
        var window = new Window(_services.GetRequiredService<AppShell>());

        // System bars can only be painted once there is a platform window, which is after the
        // constructor has run. Activated rather than Created because Android restores its own bar
        // colours across a resume, so they have to be re-asserted each time the app comes forward.
        window.Activated += (_, _) => _theme.ApplySystemBars();

        return window;
    }
}
