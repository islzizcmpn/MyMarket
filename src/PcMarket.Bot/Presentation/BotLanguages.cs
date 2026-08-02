using PcMarket.Application.Abstractions.Localization;

namespace PcMarket.Bot.Presentation;

/// <summary>The languages the bot speaks. The codes come from <see cref="LanguageCodes"/>, so the bot can
/// never offer a language the catalog cannot be translated into.
///
/// Note the fallback differs from <see cref="LanguageCodes.Fallback"/>: that one is English because English is
/// what an untranslated database row holds, whereas a chat with no stored preference should open in Russian —
/// the same default the storefront uses.</summary>
public static class BotLanguages
{
    public const string Default = "ru";

    /// <summary>Code paired with the label shown in the language panel, in display order. Labels are written in
    /// the language they select, so a customer who cannot read the current one can still find their own.</summary>
    public static readonly (string Code, string Label)[] All =
    [
        ("ru", "🇷🇺 Русский"),
        ("uz", "🇺🇿 O‘zbekcha"),
        ("en", "🇬🇧 English")
    ];

    public static bool IsSupported(string? code) => LanguageCodes.IsSupported(code);

    /// <summary>Maps anything — a full locale such as <c>ru-RU</c>, Telegram's <c>language_code</c>, an
    /// unsupported language, or nothing at all — onto a language the bot speaks.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Default;
        }

        var primary = code.Split('-')[0].ToLowerInvariant();
        return IsSupported(primary) ? primary : Default;
    }

    public static string Label(string culture)
    {
        var normalized = Normalize(culture);
        return All.First(language => language.Code == normalized).Label;
    }
}
