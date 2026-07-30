using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Users;
using PcMarket.Domain.Notifications;
using DomainEnums = PcMarket.Domain.Enums;

namespace PcMarket.Application.Users;

/// <summary>Registry of the push tokens a user's devices are reachable at. Registration is idempotent: the
/// app re-registers on every launch, and a token that has moved to another account (shared or reinstalled
/// device) is reassigned rather than duplicated.</summary>
public sealed class DeviceTokenService(IApplicationDbContext db)
{
    public async Task RegisterAsync(
        Guid userId,
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var platform = (DomainEnums.DevicePlatform)request.Platform;
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == request.Token, cancellationToken);

        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                Token = request.Token,
                Platform = platform
            });
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.LastSeenAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Drops a token on sign-out. Only the owning user may remove it; returns whether one was removed.</summary>
    public async Task<bool> UnregisterAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var existing = await db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.UserId == userId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        db.DeviceTokens.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
