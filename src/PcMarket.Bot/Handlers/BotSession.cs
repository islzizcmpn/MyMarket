using PcMarket.Application.Abstractions.Identity;

namespace PcMarket.Bot.Handlers;

/// <summary>Resolves who the Telegram user is on the store side. Unlinked users still get a working cart —
/// a guest cart keyed by their Telegram id — which is merged into their account when they link.</summary>
public sealed class BotSession(ITelegramLinkStore links)
{
    public Task<TelegramLink?> GetLinkAsync(BotContext context, CancellationToken cancellationToken = default) =>
        links.FindByTelegramUserIdAsync(context.TelegramUserId, cancellationToken);

    /// <summary>The (userId, cartToken) pair to pass to <c>CartService</c>: exactly one is non-null.</summary>
    public async Task<(Guid? UserId, string? CartToken)> ResolveCartOwnerAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var link = await GetLinkAsync(context, cancellationToken);
        return link is null ? (null, context.GuestCartToken) : (link.UserId, null);
    }
}
