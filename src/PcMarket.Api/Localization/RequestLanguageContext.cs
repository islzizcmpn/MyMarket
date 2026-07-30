using System.Globalization;
using PcMarket.Application.Abstractions.Localization;

namespace PcMarket.Api.Localization;

/// <summary>Supplies the Application layer with the language for the current request. The value is whatever
/// <c>UseRequestLocalization</c> resolved from <c>Accept-Language</c> (or the <c>?culture=</c> override),
/// normalized onto a supported code so services never have to defend against unexpected input.</summary>
public sealed class RequestLanguageContext : ILanguageContext
{
    public string Culture => LanguageCodes.Normalize(CultureInfo.CurrentUICulture.Name);
}
