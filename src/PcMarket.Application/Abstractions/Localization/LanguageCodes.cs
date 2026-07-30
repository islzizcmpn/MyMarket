using PcMarket.Contracts.Common;

namespace PcMarket.Application.Abstractions.Localization;

/// <summary>The languages content can be stored in, and the rule for falling back between them. The codes come
/// from <see cref="ContentLanguages"/> so the API and its clients cannot drift apart.</summary>
public static class LanguageCodes
{
    /// <summary>Used when a translation is missing. Entities keep their canonical English text in their own
    /// column, so this is also what an untranslated field resolves to. Note this is deliberately not the
    /// storefront's *default* language (Russian) — that is a UI concern, this is a data concern.</summary>
    public const string Fallback = ContentLanguages.Fallback;

    public static readonly string[] Supported = ContentLanguages.All;

    public static bool IsSupported(string? code) =>
        code is not null && Supported.Contains(code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps anything the caller sent — a full culture like <c>ru-RU</c>, an unsupported language, or
    /// nothing at all — onto a supported code.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Fallback;
        }

        var primary = code.Split('-')[0].ToLowerInvariant();
        return IsSupported(primary) ? primary : Fallback;
    }
}
