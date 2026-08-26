namespace PcMarket.Mobile.Services.Motion;

/// <summary>
/// The storefront's card hover, translated to touch. The card rises while a finger is down and settles
/// back when it lifts, on the two curves in <see cref="BrandEasing"/> and over the same durations the
/// web uses.
/// <para>
/// Attach it to the element that should move — normally a card's root <c>Border</c> — and drive it
/// from whatever the view uses to detect a press:
/// <code>
/// &lt;Border.Behaviors&gt;&lt;motion:CardLift x:Name="Lift" /&gt;&lt;/Border.Behaviors&gt;
/// </code>
/// It does not listen for the press itself, and that is deliberate. `PointerGestureRecognizer` was
/// the obvious source and it is the wrong one: measured on the target device, Android never raises
/// `PointerPressed` or `PointerReleased` for a *finger*, only for a real pointer device, so a lift
/// hung off it is silently dead on the only hardware that matters. A `Button`'s `Pressed`/`Released`
/// pair does fire on touch, so the view owns the press and calls in here.
/// </para>
/// <para>
/// The behaviour never handles the tap either. Navigation stays with the view, so a press that is
/// still animating opens the product immediately rather than waiting for the card to settle.
/// </para>
/// </summary>
public sealed class CardLift : Behavior<View>
{
    /// <summary>Rise, in device-independent units. The web's <c>lift-distance</c> token.</summary>
    private const double LiftDistance = -16;

    /// <summary>The web's <c>lift-in</c> token.</summary>
    private const uint LiftInMs = 500;

    /// <summary>
    /// The web's <c>lift-out</c> token. Longer than the rise on purpose: the card leaves quickly and
    /// returns slowly, which is what makes the settle feel weighted rather than springy.
    /// </summary>
    private const uint LiftOutMs = 620;

    /// <summary>
    /// Colour keys for the lift shadow, declared in <c>Motion.xaml</c>.
    /// </summary>
    private const string ShadowColorKey = "ShadowLift";
    private const string ShadowColorLightKey = "ShadowLiftLight";
    private const string ShadowOpacityKey = "ShadowLiftOpacity";
    private const string ShadowOpacityLightKey = "ShadowLiftOpacityLight";

    private View? _view;
    private Shadow? _lift;
    private bool _raised;

    /// <summary>Rise. Call from the view's press event; ignored when motion is gated off.</summary>
    public void Raise() => Animate(raise: true);

    /// <summary>Settle back. Call from the view's release event, and from anything that ends the
    /// press without one — a cancelled gesture leaves the card stranded in the air otherwise.</summary>
    public void Release() => Animate(raise: false);

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        // Requirement: when the OS says animations are off, the static state is the whole behaviour.
        // _view stays null, so Raise and Release become no-ops and there is nothing to tear down.
        if (!MotionGate.Enabled)
        {
            return;
        }

        _view = bindable;
        _lift = BuildLiftShadow();
        bindable.Unloaded += OnUnloaded;
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.Unloaded -= OnUnloaded;

        Reset(bindable);

        _view = null;
        _lift = null;

        base.OnDetachingFrom(bindable);
    }

    /// <summary>
    /// Animating a view that has left the tree throws, and a card in a scrolling list can be recycled
    /// mid-lift, so an in-flight animation is cancelled when the view really goes and the card is put
    /// back in its resting state by hand.
    /// </summary>
    /// <remarks>
    /// <c>Unloaded</c> alone is not proof that it went. Assigning <c>Shadow</c> re-attaches the
    /// platform view when the card sits in a <c>CollectionView</c>, so the rise below raises
    /// <c>Unloaded</c> for the very card under the finger — measured on the target device, with the
    /// handler and the parent both still live. Resetting on that cancelled the animation that caused
    /// it, one frame in, and the lift silently did nothing at all. The handler is what actually tells
    /// the two apart: a destroyed container has none, a re-attached one does.
    /// </remarks>
    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (sender is View { Handler: null } view)
        {
            Reset(view);
        }
    }

    private void Animate(bool raise)
    {
        if (_view is not { } view || _raised == raise)
        {
            return;
        }

        _raised = raise;
        _ = RunAsync(view, raise);
    }

    private async Task RunAsync(View view, bool raise)
    {
        try
        {
            // A press arriving mid-settle (or the reverse) has to take the view over rather than
            // queue behind it, or the card lands somewhere between the two positions.
            view.CancelAnimations();

            if (raise)
            {
                // The shadow appears with the rise and only then. One per card at rest, across a
                // scrolling list, is measurably expensive on the target device; one at a time is not.
                if (_lift is { } lift)
                {
                    view.Shadow = lift;
                }

                await view.TranslateToAsync(0, LiftDistance, LiftInMs, BrandEasing.LiftIn);
                return;
            }

            await view.TranslateToAsync(0, 0, LiftOutMs, BrandEasing.LiftOut);

            // Another press may have started during the settle, in which case the shadow belongs to
            // that lift and must not be cleared out from under it.
            if (!_raised)
            {
                ClearShadow(view);
            }
        }
        catch
        {
            // The view was torn down while the animation was in flight. Unloaded normally catches
            // this first; when the ordering goes the other way there is nothing left to restore.
        }
    }

    /// <summary>Puts the card back in its resting state immediately, without animating.</summary>
    private void Reset(View view)
    {
        _raised = false;

        try
        {
            view.CancelAnimations();
            view.TranslationY = 0;
            ClearShadow(view);
        }
        catch
        {
            // Same case as above: nothing to reset on a view whose handler has already gone.
        }
    }

    /// <summary>Drops the lift shadow. Clearing the local value rather than assigning null, because
    /// MAUI 10 types the property as non-nullable even though its default is no shadow at all.</summary>
    private static void ClearShadow(View view) => view.ClearValue(VisualElement.ShadowProperty);

    /// <summary>
    /// The web's <c>shadow-lift</c> reduced to what MAUI can paint.
    /// <para>
    /// CSS stacks three layers — a 1px light top edge, a wide dark drop, and a warm brand-tinted
    /// bloom under it — and MAUI models exactly one shadow per element, so only the dark drop
    /// survives. The hairline and the warm layer are the two that read as "lit"; losing them makes
    /// the lifted card read as raised rather than as picked out by a light, which is the accepted
    /// approximation. The <c>-36px</c> spread has no MAUI equivalent either, so the blur is reduced
    /// in its place rather than carried over at full width.
    /// </para>
    /// <para>
    /// The offset matches the rise, so the card appears to leave the shadow behind on the page.
    /// </para>
    /// </summary>
    private static Shadow BuildLiftShadow()
    {
        var shadow = new Shadow
        {
            Offset = new Point(0, -LiftDistance),
            Radius = 28,
        };

        shadow.SetAppTheme(Shadow.BrushProperty, LightBrush(), DarkBrush());

        // Alpha rides Opacity rather than the brush colour: Android composites the shadow through
        // its own paint, and a brush that already carries alpha ends up multiplied twice.
        shadow.SetAppTheme(
            Shadow.OpacityProperty,
            (float)Resource(ShadowOpacityLightKey, 0.34),
            (float)Resource(ShadowOpacityKey, 0.92));

        return shadow;

        Brush DarkBrush() => new SolidColorBrush(Resource(ShadowColorKey, Colors.Black));
        Brush LightBrush() => new SolidColorBrush(Resource(ShadowColorLightKey, Colors.Black));
    }

    /// <summary>Reads a value out of the merged dictionaries, so the shadow is retuned in
    /// <c>Motion.xaml</c> rather than here. The fallback only ever applies if that file stops being
    /// merged, in which case a plain black shadow is still better than none.</summary>
    private static T Resource<T>(string key, T fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is T typed
            ? typed
            : fallback;
}
