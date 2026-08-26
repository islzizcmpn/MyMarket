namespace PcMarket.Mobile.Services.Motion;

/// <summary>
/// The storefront's scroll-reveal entrance. An item starts a little low and fully transparent, then
/// rises into place as it appears, with each item in a batch starting slightly after the one before so
/// a grid cascades rather than arriving as a block.
/// <para>
/// The web arms this with an <c>IntersectionObserver</c>, which MAUI has no counterpart to. The hook
/// here is the item container's <c>Loaded</c> event, and it fires once per container rather than on
/// every recycle: a card that scrolls away and comes back is already revealed and stays put.
/// </para>
/// </summary>
public sealed class RevealBehavior : Behavior<View>
{
    /// <summary>The web's <c>reveal-rise</c> token.</summary>
    private const double Rise = 24;

    /// <summary>The web's <c>reveal-dur</c> token.</summary>
    private const uint DurationMs = 620;

    /// <summary>Gap between one item's entrance and the next in the same batch.</summary>
    private const int StaggerStepMs = 70;

    /// <summary>
    /// Cap on the cascade. A page of twenty cards stepped all the way out would leave the last one
    /// arriving well over a second late, which reads as a stall rather than as choreography.
    /// </summary>
    private const int MaxStaggerSteps = 6;

    /// <summary>
    /// Two containers loading within this window are treated as one batch. A grid inflates its
    /// visible rows in a single layout pass, so the whole first screen falls inside it; anything
    /// loading later — a page appended by paging, a card recycled on scroll — starts a fresh batch
    /// and enters immediately rather than inheriting a stale delay.
    /// </summary>
    private const int BatchWindowMs = 150;

    /// <summary>
    /// Batch state, shared across every container currently arming. Only ever touched from the
    /// <c>Loaded</c> handler, which the platform raises on the UI thread, so no lock is needed.
    /// The alternative — passing each item its index — would make the behaviour impossible to attach
    /// from a <c>DataTemplate</c>, which is the only place it is used.
    /// </summary>
    private static DateTime _batchStamp = DateTime.MinValue;
    private static int _batchPosition;

    private View? _view;
    private bool _revealed;
    private bool _detached;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        // With motion off the item is simply already where it belongs; nothing is hidden, so there
        // is no way for it to be stranded invisible.
        if (!MotionGate.Enabled)
        {
            return;
        }

        _view = bindable;
        _detached = false;

        // Set the entrance state during attach, before the first paint, so the item is never seen in
        // its final position for a frame and then yanked back down.
        bindable.Opacity = 0;
        bindable.TranslationY = Rise;

        bindable.Loaded += OnLoaded;
        bindable.Unloaded += OnUnloaded;

        // Belt and braces against the worst failure this behaviour has: an item hidden for its
        // entrance whose entrance never arms is invisible for good. HandlerChanged fires when the
        // platform view is created, which is unconditional for anything that renders, so between
        // the two hooks there is no path that leaves an item at zero opacity. Arming is idempotent.
        bindable.HandlerChanged += OnLoaded;

        if (bindable.IsLoaded || bindable.Handler is not null)
        {
            OnLoaded(bindable, EventArgs.Empty);
        }
    }

    protected override void OnDetachingFrom(View bindable)
    {
        _detached = true;
        bindable.Loaded -= OnLoaded;
        bindable.Unloaded -= OnUnloaded;
        bindable.HandlerChanged -= OnLoaded;

        Show(bindable);

        _view = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_revealed || sender is not View view)
        {
            return;
        }

        _revealed = true;
        _ = RunAsync(view, NextDelay());
    }

    /// <summary>
    /// Cancels an entrance that is still running and leaves the item visible. A card torn down
    /// mid-reveal would otherwise be recycled still carrying a partial opacity and offset.
    /// </summary>
    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (sender is View view)
        {
            Show(view);
        }
    }

    private async Task RunAsync(View view, int delayMs)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);

                if (_detached || _view is null)
                {
                    return;
                }
            }

            // Fade and rise together rather than in sequence: they are one movement, and awaiting
            // them one after the other would double the duration the token names.
            await Task.WhenAll(
                view.FadeToAsync(1, DurationMs, BrandEasing.Reveal),
                view.TranslateToAsync(0, 0, DurationMs, BrandEasing.Reveal));
        }
        catch
        {
            // The container went away mid-entrance. Unloaded has already restored the static state.
        }
    }

    /// <summary>Position of this item in the current batch, converted to a start delay.</summary>
    private static int NextDelay()
    {
        var now = DateTime.UtcNow;

        _batchPosition = now - _batchStamp <= TimeSpan.FromMilliseconds(BatchWindowMs)
            ? Math.Min(_batchPosition + 1, MaxStaggerSteps)
            : 0;

        _batchStamp = now;

        return _batchPosition * StaggerStepMs;
    }

    private static void Show(View view)
    {
        try
        {
            view.CancelAnimations();
            view.Opacity = 1;
            view.TranslationY = 0;
        }
        catch
        {
            // Nothing to restore on a view whose handler has already been disconnected.
        }
    }
}
