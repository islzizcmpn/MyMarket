namespace PcMarket.Api.Auth;

/// <summary>JWT bearer settings, bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "pcmarket";
    public string Audience { get; set; } = "pcmarket-clients";

    /// <summary>HMAC signing key; must be at least 32 bytes. Override via secret/env in non-dev environments.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
