namespace PcMarket.Application.Abstractions.Localization;

/// <summary>The language the current request should be answered in. Implemented in the host from whatever
/// carries the caller's language (the API reads <c>CultureInfo.CurrentUICulture</c>, set from
/// <c>Accept-Language</c>), so use-case services never touch ambient culture state directly.</summary>
public interface ILanguageContext
{
    /// <summary>Supported two-letter code; never null and never unsupported — see <see cref="LanguageCodes"/>.</summary>
    string Culture { get; }
}
