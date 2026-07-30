namespace PcMarket.Contracts.Auth;

public sealed record RegisterRequest(string Phone, string Password, string? FullName);

/// <summary>Result of a registration request; the OTP is delivered out-of-band via SMS.</summary>
public sealed record RegisterResponse(string Phone, bool OtpSent);

public sealed record VerifyOtpRequest(string Phone, string Code);

public sealed record LoginRequest(string Phone, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>Issued access + refresh token pair.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    IReadOnlyList<string> Roles);
