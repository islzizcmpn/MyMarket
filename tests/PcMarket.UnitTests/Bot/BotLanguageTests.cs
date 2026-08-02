using PcMarket.Bot.Presentation;

namespace PcMarket.UnitTests.Bot;

/// <summary>The language panel and the rules behind it. Russian is the default the bot opens in, so anything
/// it cannot make sense of — an unsupported locale, a malformed button payload, nothing at all — has to land
/// there rather than on the English fallback the *data* layer uses.</summary>
public class BotLanguageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr")]
    [InlineData("pt-BR")]
    [InlineData("nonsense")]
    public void Normalize_FallsBackToRussian(string? code) =>
        Assert.Equal("ru", BotLanguages.Normalize(code));

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("uz", "uz")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("uz-Latn-UZ", "uz")]
    [InlineData("en-GB", "en")]
    public void Normalize_KeepsASupportedLanguage(string code, string expected) =>
        Assert.Equal(expected, BotLanguages.Normalize(code));

    [Fact]
    public void Panel_OffersEveryLanguage_AndTicksTheCurrentOne()
    {
        var labels = Labels(BotKeyboards.Languages("uz"));

        Assert.Contains(labels, label => label.Contains("Русский", StringComparison.Ordinal) && !label.StartsWith("✅", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains("O‘zbekcha", StringComparison.Ordinal) && label.StartsWith("✅", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains("English", StringComparison.Ordinal) && !label.StartsWith("✅", StringComparison.Ordinal));
    }

    [Fact]
    public void Panel_TicksRussian_ForAChatThatNeverChose()
    {
        var labels = Labels(BotKeyboards.Languages(BotLanguages.Default));

        Assert.Contains(labels, label => label.StartsWith("✅", StringComparison.Ordinal) && label.Contains("Русский", StringComparison.Ordinal));
    }

    [Fact]
    public void MainMenu_CarriesTheLanguageButton_InEveryLanguage()
    {
        foreach (var (code, _) in BotLanguages.All)
        {
            var labels = Labels(BotKeyboards.MainMenu(code, isLinked: true));
            Assert.Contains(labels, label => label.Contains(BotPhrases.Get(code, Phrase.MenuLanguage), StringComparison.Ordinal));
        }
    }

    /// <summary>Every phrase must exist in all three languages: a blank one would reach a customer as an empty
    /// message, and Telegram rejects those outright.</summary>
    [Fact]
    public void EveryPhrase_IsTranslatedIntoEveryLanguage()
    {
        foreach (var phrase in Enum.GetValues<Phrase>())
        {
            foreach (var (code, _) in BotLanguages.All)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(BotPhrases.Get(code, phrase)),
                    $"Phrase {phrase} has no {code} translation.");
            }
        }
    }

    [Fact]
    public void Money_IsWrittenInTheReadersCurrencyWord()
    {
        Assert.Equal("650 000 сум", BotText.Money("ru", 650_000m));
        Assert.Equal("650 000 so‘m", BotText.Money("uz", 650_000m));
        Assert.Equal("650 000 UZS", BotText.Money("en", 650_000m));
    }

    private static List<string> Labels(Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup markup) =>
        markup.InlineKeyboard.SelectMany(row => row).Select(button => button.Text).ToList();
}
