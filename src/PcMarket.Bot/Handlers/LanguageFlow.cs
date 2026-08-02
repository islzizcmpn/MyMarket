using PcMarket.Bot.Localization;
using PcMarket.Bot.Presentation;

namespace PcMarket.Bot.Handlers;

/// <summary>The language panel. Selecting a language stores it (on the account once one is linked, see
/// <see cref="BotLanguageService"/>) and answers in the new language immediately, so the change is visible in
/// the same message the customer just tapped.</summary>
public sealed class LanguageFlow(BotLanguageService languages, BotSession session, BotResponder responder)
{
    public Task ShowPanelAsync(BotContext context, CancellationToken cancellationToken = default) =>
        responder.ReplyAsync(
            context,
            BotPhrases.Get(context.Culture, Phrase.LanguagePanel),
            BotKeyboards.Languages(context.Culture),
            cancellationToken);

    /// <param name="code">Language code from the pressed button; anything unsupported falls back to the
    /// bot's default rather than failing the update.</param>
    public async Task SetLanguageAsync(BotContext context, string? code, CancellationToken cancellationToken = default)
    {
        var culture = BotLanguages.Normalize(code);
        await languages.SetAsync(context.TelegramUserId, culture, cancellationToken);

        var localized = context with { Culture = culture };
        await responder.AcknowledgeAsync(localized, BotPhrases.Get(culture, Phrase.LanguageChangedToast), cancellationToken);

        var link = await session.GetLinkAsync(localized, cancellationToken);
        var confirmation = BotPhrases.Format(culture, Phrase.LanguageChanged, BotLanguages.Label(culture));

        await responder.ReplyAsync(
            localized,
            $"{confirmation}\n\n{BotText.MainMenu(culture, link?.Phone)}",
            BotKeyboards.MainMenu(culture, link is not null),
            cancellationToken);
    }
}
