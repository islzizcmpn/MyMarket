using System.Globalization;

namespace PcMarket.Web.Services;

/// <summary>Presentation helpers for the storefront.</summary>
public static class Format
{
    private static readonly CultureInfo Uz = CreateUzCulture();

    /// <summary>Formats an amount as Uzbek som with space thousands separators, e.g. <c>7 500 000 so'm</c>.</summary>
    public static string Money(decimal amount) => $"{amount.ToString("#,0", Uz)} so‘m";

    private static CultureInfo CreateUzCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalDigits = 0;
        return culture;
    }
}
