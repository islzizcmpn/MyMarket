namespace PcMarket.Mobile.Services.Motion;

/// <summary>
/// The one place that decides whether motion runs at all. Every behaviour asks first and does nothing
/// when the answer is no, leaving the static visual state on screen.
/// <para>
/// Only Android supplies an implementation; on the other heads the partial method has no body, the
/// call compiles away and motion stays on — the same shape as <see cref="SystemBars"/>.
/// </para>
/// </summary>
internal static partial class MotionGate
{
    /// <summary>
    /// Read once and kept, because a behaviour asks on every attach and a card list attaches dozens
    /// in a scroll: the underlying read crosses into the platform settings provider. The cost of
    /// caching is that turning animations off mid-session takes effect on the next launch, which is
    /// how the setting is normally changed anyway.
    /// </summary>
    private static readonly Lazy<bool> Gate = new(Query, isThreadSafe: true);

    /// <summary>False when the OS reports that animations are switched off.</summary>
    public static bool Enabled => Gate.Value;

    /// <summary>Flips <paramref name="enabled"/> to false when the platform disables animation.</summary>
    static partial void QueryPlatform(ref bool enabled);

    private static bool Query()
    {
        var enabled = true;
        QueryPlatform(ref enabled);
        return enabled;
    }
}
