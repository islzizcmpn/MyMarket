namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Result of validating a presented refresh token.</summary>
public sealed record RefreshTokenValidation(bool IsValid, Guid UserId);

/// <summary>Persists refresh tokens as salted hashes so they are revocable and support rotation.</summary>
public interface IRefreshTokenStore
{
    Task StoreAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<RefreshTokenValidation> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
