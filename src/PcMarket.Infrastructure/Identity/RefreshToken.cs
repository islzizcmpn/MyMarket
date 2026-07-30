namespace PcMarket.Infrastructure.Identity;

/// <summary>A persisted refresh token, stored only as a hash so a database leak cannot reveal usable
/// tokens. Rotation revokes the presented token and issues a fresh one.</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
