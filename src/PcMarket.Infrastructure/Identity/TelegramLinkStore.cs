using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Domain.Common;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.Infrastructure.Identity;

/// <summary>Stores the Telegram-account link on <see cref="ApplicationUser.TelegramUserId"/>.</summary>
public sealed class TelegramLinkStore(PcMarketDbContext db, UserManager<ApplicationUser> userManager) : ITelegramLinkStore
{
    public async Task<TelegramLink?> FindByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new TelegramLink(user.Id, telegramUserId, user.PhoneNumber ?? user.UserName, user.FullName, [.. roles]);
    }

    public async Task<long?> GetTelegramUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TelegramUserId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task LinkAsync(Guid userId, long telegramUserId, CancellationToken cancellationToken = default)
    {
        // One Telegram account maps to at most one store account: release any previous holder first.
        var previous = await db.Users
            .Where(u => u.TelegramUserId == telegramUserId && u.Id != userId)
            .ToListAsync(cancellationToken);
        foreach (var user in previous)
        {
            user.TelegramUserId = null;
        }

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                     ?? throw new DomainException("Account not found.");
        target.TelegramUserId = telegramUserId;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UnlinkAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.Where(u => u.TelegramUserId == telegramUserId).ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return false;
        }

        foreach (var user in users)
        {
            user.TelegramUserId = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
