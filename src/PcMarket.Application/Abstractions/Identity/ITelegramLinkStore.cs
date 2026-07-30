namespace PcMarket.Application.Abstractions.Identity;

/// <summary>A user account bound to a Telegram user id, with the roles that decide what the bot lets them do.</summary>
public sealed record TelegramLink(
    Guid UserId,
    long TelegramUserId,
    string? Phone,
    string? FullName,
    IReadOnlyList<string> Roles);

/// <summary>Reads and writes the Telegram-account link on a user. Implemented over the Identity store so the
/// Application layer (and the bot) never touches Identity types directly.</summary>
public interface ITelegramLinkStore
{
    Task<TelegramLink?> FindByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>The Telegram user id (also the private chat id) linked to a user, or null when unlinked.</summary>
    Task<long?> GetTelegramUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Binds a Telegram user to an account. Any other account holding the same Telegram id is unbound,
    /// so the relationship stays one-to-one.</summary>
    Task LinkAsync(Guid userId, long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes the link for a Telegram user; returns whether one existed.</summary>
    Task<bool> UnlinkAsync(long telegramUserId, CancellationToken cancellationToken = default);
}
