namespace PcMarket.Application.Abstractions.Identity;

/// <summary>A signed access token and its expiry.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Minimal user identity needed to mint a token.</summary>
public sealed record TokenUser(Guid Id, string UserName, IReadOnlyCollection<string> Roles);

/// <summary>Issues JWT access tokens and cryptographically-random refresh tokens.</summary>
public interface ITokenService
{
    AccessToken IssueAccessToken(TokenUser user);

    string GenerateRefreshToken();

    TimeSpan RefreshTokenLifetime { get; }
}
