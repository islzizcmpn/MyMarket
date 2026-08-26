using Android.Provider;

namespace PcMarket.Mobile.Services.Motion;

internal static partial class MotionGate
{
    static partial void QueryPlatform(ref bool enabled)
    {
        try
        {
            var resolver = Android.App.Application.Context.ContentResolver;
            if (resolver is null)
            {
                return;
            }

            // Developer options and the accessibility "remove animations" toggle both drive this to
            // zero. Transition and window scales exist too, but the animator scale is the one that
            // governs property animation, which is what TranslateTo and FadeTo compile down to.
            var scale = Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f);
            enabled = scale > 0f;
        }
        catch
        {
            // An unreadable settings provider is not a reason to ship a static app; motion stays on.
        }
    }
}
