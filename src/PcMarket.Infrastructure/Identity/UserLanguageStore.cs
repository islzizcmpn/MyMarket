using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.Infrastructure.Identity;

/// <summary>Stores the chosen language on <see cref="ApplicationUser.Language"/>, so it follows the account
/// across every client rather than living in one channel's session state.</summary>
public sealed class UserLanguageStore(PcMarketDbContext db) : IUserLanguageStore
{
    public async Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Language)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
        await db.Users
            .Where(u => u.TelegramUserId == telegramUserId)
            .Select(u => u.Language)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetAsync(Guid userId, string culture, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.Language == culture)
        {
            return;
        }

        user.Language = culture;
        await db.SaveChangesAsync(cancellationToken);
    }
}
