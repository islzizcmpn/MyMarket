namespace PcMarket.Mobile.Services;

/// <summary>Applies and persists the light/dark choice, mirroring the storefront's
/// <c>localStorage['pcmarket.theme']</c>. Dark is the default when nothing is stored — the app
/// deliberately does not follow the OS theme, because the web does not either.</summary>
public sealed class ThemeService
{
    private const string PreferenceKey = "pcmarket.theme";
    private const string DarkValue = "dark";
    private const string LightValue = "light";

    /// <summary>System bars are painted from the same token as the shell chrome, resolved out of
    /// the merged dictionaries so <c>Colors.xaml</c> stays the single source for the value.</summary>
    private const string BarColorKey = "TokenSurface";
    private const string BarColorLightKey = "TokenSurfaceLight";

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    /// <summary>Reads the stored choice and applies it. Called from the <see cref="App"/> constructor,
    /// so every <c>AppThemeBinding</c> resolves against the right theme on the first paint rather
    /// than flashing the wrong one.</summary>
    public void Restore() => Set(Read() == LightValue ? AppTheme.Light : AppTheme.Dark);

    /// <summary>Applies a chosen theme and remembers it. Every open page updates immediately:
    /// setting <c>UserAppTheme</c> re-evaluates the theme bindings in place.</summary>
    public void Apply(AppTheme theme)
    {
        Set(theme);
        Write(Current == AppTheme.Light ? LightValue : DarkValue);
        ApplySystemBars();
    }

    /// <summary>Repaints the system bars from the active theme. Separate from <see cref="Apply"/>
    /// because the platform window does not exist yet while the app is being constructed, and
    /// because Android resets the bars behind the app across a resume.</summary>
    public void ApplySystemBars()
    {
        if (ResolveBarColor() is { } background)
        {
            SystemBars.Apply(background, Current == AppTheme.Dark);
        }
    }

    private void Set(AppTheme theme)
    {
        Current = theme == AppTheme.Light ? AppTheme.Light : AppTheme.Dark;

        if (Application.Current is { } app)
        {
            app.UserAppTheme = Current;
        }
    }

    private Color? ResolveBarColor()
    {
        var key = Current == AppTheme.Light ? BarColorLightKey : BarColorKey;

        return Application.Current?.Resources.TryGetValue(key, out var value) == true
            ? value as Color
            : null;
    }

    private static string Read()
    {
        try
        {
            return Preferences.Default.Get(PreferenceKey, DarkValue);
        }
        catch
        {
            // Blocked or unavailable storage must never hold up start-up; dark is the default anyway.
            return DarkValue;
        }
    }

    private static void Write(string theme)
    {
        try
        {
            Preferences.Default.Set(PreferenceKey, theme);
        }
        catch
        {
            // The choice still applies for this run; it simply will not survive a restart.
        }
    }
}
