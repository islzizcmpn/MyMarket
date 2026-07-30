using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.Infrastructure.Identity;

/// <summary>Stores refresh tokens as SHA-256 hashes and supports validation, revocation, and rotation.</summary>
public sealed class RefreshTokenStore(PcMarketDbContext db) : IRefreshTokenStore
{
    public async Task StoreAsync(
        Guid userId,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(refreshToken),
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshTokenValidation> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        return token is not null && token.IsActive(DateTimeOffset.UtcNow)
            ? new RefreshTokenValidation(true, token.UserId)
            : new RefreshTokenValidation(false, Guid.Empty);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
