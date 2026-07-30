namespace PcMarket.Contracts.Common;

/// <summary>The languages storefront content is published in. Shared so the API, the back office and any other
/// client agree on the codes without each keeping its own list.</summary>
public static class ContentLanguages
{
    /// <summary>Language the entities' own columns are written in, and what a missing translation falls back to.</summary>
    public const string Fallback = "en";

    public static readonly string[] All = ["ru", "uz", "en"];

    /// <summary>Languages that need a stored translation — every supported language except the one the canonical
    /// column already holds. These are the fields the back office edits.</summary>
    public static readonly string[] Translatable = ["ru", "uz"];

    public static string DisplayName(string culture) => culture switch
    {
        "ru" => "Russian",
        "uz" => "Uzbek",
        "en" => "English",
        _ => culture
    };
}

/// <summary>One translated field value. <c>Value</c> is never blank — an absent entry means "not translated",
/// which resolves to the entity's canonical English column.</summary>
public sealed record TranslationDto(string Field, string Culture, string Value);
