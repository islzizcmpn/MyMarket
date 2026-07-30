using PcMarket.Contracts.Auth;

namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Outcome of an authentication attempt: either an issued token pair or an error message.</summary>
public sealed record AuthOutcome(bool Succeeded, string? Error, AuthResponse? Response)
{
    public static AuthOutcome Ok(AuthResponse response) => new(true, null, response);

    public static AuthOutcome Fail(string error) => new(false, error, null);
}

/// <summary>Phone-first authentication: register + OTP verification, login, refresh-token rotation, logout.</summary>
public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Finds or creates an account for a phone number whose ownership has <em>already</em> been proven
    /// by an outside authority, and marks it confirmed. No code is generated and no SMS is sent, because there
    /// is nothing left to prove.
    ///
    /// The only caller today is the Telegram contact share: Telegram verified that number when the account was
    /// created and hands it over from its own servers, so it is stronger evidence than an SMS round trip. Do not
    /// call this with a number the user merely typed — see <see cref="RegisterAsync"/> for that.</summary>
    /// <returns>The id of the confirmed account.</returns>
    Task<Guid> RegisterVerifiedAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthOutcome> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthOutcome> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
