namespace PcMarket.Web.Components.Layout.Shell;

/// <summary>Single source for the contact details the shell repeats across the top bar, the floating
/// rail and the footer. Everything here is a <b>placeholder</b> pending the real values — swap these
/// constants rather than editing the three components.</summary>
public static class StoreContact
{
    /// <summary>Placeholder number. Display form and <c>tel:</c> form are kept separate so formatting
    /// changes never break the dial link.</summary>
    public const string Phone = "+998000000000";

    public const string PhoneHref = "tel:+998000000000";

    /// <summary>Real showroom address (kept in English here; the localized copies live in the resx
    /// files under <c>Shell.Address</c>).</summary>
    public const string Address =
        "Uzbekistan, Tashkent, Yunusabad district, 13th quarter, 2A, Trade Complex \"Lion\", Landmark \"Mega Planet\"";

    /// <summary>
    /// Deliberately a dummy address on the IANA-reserved <c>example.com</c> documentation domain, so
    /// it can never resolve to a real inbox. The reference site's address is intentionally not
    /// reused. Swap for the store's real mailbox when there is one.
    /// </summary>
    public const string Email = "info@example.com";

    public const string EmailHref = "mailto:info@example.com";

    /// <summary>Placeholder channel for the "Order on Telegram" call to action.</summary>
    public const string TelegramOrderUrl = "https://t.me/pcmarket_uz";

    /// <summary>Real reviews channel — also used by the home page testimonials block (Phase 13).</summary>
    public const string TelegramReviewsUrl = "https://t.me/otzivPCmarket";

    public const string InstagramUrl = "https://instagram.com/";

    public const string FacebookUrl = "https://facebook.com/";
}
