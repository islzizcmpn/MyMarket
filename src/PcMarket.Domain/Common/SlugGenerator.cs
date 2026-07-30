using System.Globalization;
using System.Text;

namespace PcMarket.Domain.Common;

/// <summary>Produces URL-safe slugs from arbitrary product/category names.</summary>
public static class SlugGenerator
{
    public static string Generate(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                builder.Append(ch);
                previousDash = false;
            }
            else if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
