using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Presentation;

namespace PcMarket.Bot.Localization;

/// <summary>Decides which language to answer a Telegram user in, and remembers what they pick.
///
/// The account is the home of the preference, so a customer who switches to Uzbek in the bot finds the
/// storefront in Uzbek too. A chat with no linked account has nowhere to keep it, so it falls back to Redis
/// until the account exists — at which point <see cref="AdoptAsync"/> carries the choice over.</summary>
public sealed class BotLanguageService(
    ITelegramLinkStore links,
    IUserLanguageStore userLanguages,
    IConversationStore conversations)
{
    /// <param name="telegramLanguageCode">The <c>language_code</c> Telegram reports for the sender. It decides
    /// the language of a first-ever message — someone whose Telegram is in Uzbek should not have to find the
    /// language panel in Russian to say so — and is ignored from the moment they choose for themselves.</param>
    public async Task<string> ResolveAsync(
        long telegramUserId,
        string? telegramLanguageCode,
        CancellationToken cancellationToken = default)
    {
        var chosen = await userLanguages.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
                     ?? await conversations.GetLanguageAsync(telegramUserId, cancellationToken);

        // Normalize covers all three cases: a stored code, Telegram's locale (which may be anything, e.g.
        // "pt-BR"), and nothing at all — the last two land on Russian.
        return BotLanguages.Normalize(chosen ?? telegramLanguageCode);
    }

    public async Task SetAsync(long telegramUserId, string culture, CancellationToken cancellationToken = default)
    {
        var normalized = BotLanguages.Normalize(culture);
        await conversations.SetLanguageAsync(telegramUserId, normalized, cancellationToken);

        var link = await links.FindByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (link is not null)
        {
            await userLanguages.SetAsync(link.UserId, normalized, cancellationToken);
        }
    }

    /// <summary>Moves a guest's choice onto the account they just linked, unless the account already carries one
    /// — a preference set on the account elsewhere outranks whatever this chat happened to default to.</summary>
    public async Task AdoptAsync(Guid userId, string culture, CancellationToken cancellationToken = default)
    {
        var existing = await userLanguages.GetAsync(userId, cancellationToken);
        if (existing is null)
        {
            await userLanguages.SetAsync(userId, BotLanguages.Normalize(culture), cancellationToken);
        }
    }
}
