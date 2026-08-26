namespace PcMarket.Mobile.Services;

/// <summary>Paints the platform's status and navigation bars. Only Android supplies an
/// implementation; on the other heads the partial method has no body and the call compiles away.</summary>
internal static partial class SystemBars
{
    /// <summary>Colours both bars and sets their icon contrast for the active theme.</summary>
    /// <param name="background">Bar fill, matching the shell chrome.</param>
    /// <param name="darkTheme">True when the app is dark, meaning the bars need light icons.</param>
    public static void Apply(Color background, bool darkTheme) => ApplyPlatform(background, darkTheme);

    static partial void ApplyPlatform(Color background, bool darkTheme);
}
