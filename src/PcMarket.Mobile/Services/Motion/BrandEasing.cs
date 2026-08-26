namespace PcMarket.Mobile.Services.Motion;

/// <summary>
/// The storefront's two motion curves, evaluated numerically so the app rises and settles on exactly
/// the coefficients <c>app.css</c> declares.
/// <para>
/// MAUI's built-in easings have no equivalent to either curve. <see cref="Easing.CubicOut"/> is the
/// nearest to the lift and is visibly different: it reaches half its distance around 30% of the way
/// through where the web's curve is already past 80%, which loses the snap the whole interaction is
/// built on. Both curves are therefore ported as coefficients rather than substituted.
/// </para>
/// </summary>
public static class BrandEasing
{
    /// <summary>
    /// The rise, from the <c>lift-ease</c> token: <c>cubic-bezier(.16, 1, .3, 1)</c>. An easeOutExpo
    /// shape — almost all the distance covered in the first third, then a long glide into rest, with
    /// no overshoot anywhere in the curve, so it never bounces.
    /// </summary>
    public static readonly Easing LiftIn = CubicBezier(0.16, 1.0, 0.3, 1.0);

    /// <summary>
    /// The settle, from the <c>lift-ease-return</c> token: <c>cubic-bezier(.65, 0, .35, 1)</c>. An
    /// easeInOutCubic shape, deliberately not the rise curve run backwards: easeOutExpo front-loads
    /// whichever direction it runs, so reusing it on the way down drops the card most of the way in
    /// the first few frames and reads as a snap-back rather than a weighted release.
    /// </summary>
    public static readonly Easing LiftOut = CubicBezier(0.65, 0.0, 0.35, 1.0);

    /// <summary>
    /// Scroll-reveal entrances run on the same curve as the lift, so entrances and interactions read
    /// as one system rather than two clocks. This mirrors the web, where the reveal transition names
    /// the lift token directly.
    /// </summary>
    public static readonly Easing Reveal = LiftIn;

    /// <summary>Newton-Raphson refinement passes before falling back to bisection.</summary>
    private const int NewtonIterations = 8;

    /// <summary>Below this the derivative is too flat for Newton to make progress reliably.</summary>
    private const double MinimumSlope = 0.001;

    /// <summary>Half a frame at 120 Hz expressed as curve distance — finer than anything visible.</summary>
    private const double Precision = 0.0000001;

    /// <summary>
    /// Builds an <see cref="Easing"/> for a CSS <c>cubic-bezier(x1, y1, x2, y2)</c>, whose first and
    /// last control points are fixed at (0,0) and (1,1).
    /// </summary>
    /// <remarks>
    /// A CSS timing function is a parametric curve, not a function of time: the animation's progress
    /// is the curve's <c>x</c>, and the value it eases to is the <c>y</c> at that <c>x</c>. So each
    /// evaluation first solves <c>x(t) = progress</c> for the curve parameter <c>t</c>, then samples
    /// <c>y(t)</c>. This is the same solve browsers use, so the ported motion matches frame for frame.
    /// </remarks>
    public static Easing CubicBezier(double x1, double y1, double x2, double y2)
    {
        // Polynomial coefficients of the cubic with endpoints pinned at 0 and 1, expanded once here
        // rather than on every sample: the curve is fixed for the lifetime of the Easing.
        var cx = 3.0 * x1;
        var bx = (3.0 * (x2 - x1)) - cx;
        var ax = 1.0 - cx - bx;

        var cy = 3.0 * y1;
        var by = (3.0 * (y2 - y1)) - cy;
        var ay = 1.0 - cy - by;

        double SampleX(double t) => (((ax * t) + bx) * t + cx) * t;
        double SampleY(double t) => (((ay * t) + by) * t + cy) * t;
        double SlopeX(double t) => ((3.0 * ax * t) + (2.0 * bx)) * t + cx;

        return new Easing(progress =>
        {
            // MAUI clamps its own progress, but a custom easing is also called directly by the
            // reveal path, so the endpoints are pinned here rather than assumed.
            if (progress <= 0.0)
            {
                return 0.0;
            }

            if (progress >= 1.0)
            {
                return 1.0;
            }

            return SampleY(SolveForT(progress));
        });

        double SolveForT(double x)
        {
            var t = x;

            for (var i = 0; i < NewtonIterations; i++)
            {
                var error = SampleX(t) - x;
                if (Math.Abs(error) < Precision)
                {
                    return t;
                }

                var slope = SlopeX(t);
                if (Math.Abs(slope) < MinimumSlope)
                {
                    break;
                }

                t -= error / slope;
            }

            // Newton can leave the unit interval on the near-vertical opening of a curve like
            // (.16, 1, .3, 1); bisection cannot, and always converges.
            var low = 0.0;
            var high = 1.0;
            t = x;

            while (high - low > Precision)
            {
                if (SampleX(t) < x)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }

                t = ((high - low) * 0.5) + low;
            }

            return t;
        }
    }
}
