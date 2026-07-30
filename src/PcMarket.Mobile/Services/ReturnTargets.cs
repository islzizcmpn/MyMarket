namespace PcMarket.Mobile.Services;

/// <summary>Where to send the user after they sign in. The auth screens carry an opaque token rather than a
/// raw Shell route so a query string can never be turned into arbitrary navigation.</summary>
public static class ReturnTargets
{
    public const string Checkout = "checkout";

    /// <param name="fallbackRoute">Where to go when no target was carried. Defaults to popping the auth
    /// screen; the OTP step overrides it, since popping would land back on the registration form.</param>
    public static Task NavigateAsync(string? returnTo, string fallbackRoute = "..") => returnTo switch
    {
        // Absolute to the cart tab, then push checkout on top of it, so Back lands on the cart.
        Checkout => Shell.Current.GoToAsync("//cart/checkout"),
        _ => Shell.Current.GoToAsync(fallbackRoute)
    };
}
