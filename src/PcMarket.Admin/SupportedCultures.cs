using PcMarket.Contracts.Common;

namespace PcMarket.Admin;

/// <summary>The languages the back office ships in — the same three the storefront and the bot speak, taken
/// from <see cref="ContentLanguages"/> so the three surfaces cannot drift apart. Russian is what the panel
/// loads in until the manager picks something else.</summary>
public static class SupportedCultures
{
    public const string Default = "ru";

    /// <summary>Culture code paired with the label shown in the switcher, in display order.</summary>
    public static readonly (string Code, string Label)[] All =
    [
        ("ru", "RU"),
        ("uz", "UZ"),
        ("en", "EN"),
    ];

    public static readonly string[] Codes = [.. All.Select(culture => culture.Code)];

    public static bool IsSupported(string? code) =>
        code is not null && Codes.Contains(code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps a stored preference — which may be null, or a full locale such as <c>uz-Latn-UZ</c> —
    /// onto a code the panel can load.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Default;
        }

        var primary = code.Split('-')[0].ToLowerInvariant();
        return IsSupported(primary) ? primary : Default;
    }
}
