namespace PcMarket.Web;

/// <summary>The languages the storefront ships in. Russian is the default the site loads in until the shopper
/// picks something else with the header switcher.</summary>
public static class SupportedCultures
{
    public const string Default = "ru";

    /// <summary>Culture code paired with the label shown in the header switcher, in display order.</summary>
    public static readonly (string Code, string Label)[] All =
    [
        ("ru", "RU"),
        ("uz", "UZ"),
        ("en", "EN"),
    ];

    public static readonly string[] Codes = [.. All.Select(culture => culture.Code)];

    public static bool IsSupported(string? code) =>
        code is not null && Codes.Contains(code, StringComparer.OrdinalIgnoreCase);
}
