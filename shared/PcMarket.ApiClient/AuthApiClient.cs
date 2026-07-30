using PcMarket.Contracts.Auth;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the authentication endpoints (phone-first register/OTP/login/refresh).</summary>
public sealed class AuthApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<RegisterRequest, RegisterResponse>("auth/register", request, cancellationToken);

    public Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<VerifyOtpRequest, AuthResponse>("auth/verify-otp", request, cancellationToken);

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<LoginRequest, AuthResponse>("auth/login", request, cancellationToken);

    public Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<RefreshRequest, AuthResponse>("auth/refresh", request, cancellationToken);

    public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) =>
        PostAsync("auth/logout", request, cancellationToken);
}
