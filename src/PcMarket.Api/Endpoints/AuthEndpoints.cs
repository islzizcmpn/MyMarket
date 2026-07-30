using PcMarket.Application.Abstractions.Identity;
using PcMarket.Contracts.Auth;

namespace PcMarket.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth").RequireRateLimiting("auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
                Results.Ok(await auth.RegisterAsync(request, ct)))
            .WithValidation<RegisterRequest>();

        group.MapPost("/verify-otp", async (VerifyOtpRequest request, IAuthService auth, CancellationToken ct) =>
                ToResult(await auth.VerifyOtpAsync(request, ct), StatusCodes.Status400BadRequest))
            .WithValidation<VerifyOtpRequest>();

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
                ToResult(await auth.LoginAsync(request, ct), StatusCodes.Status401Unauthorized))
            .WithValidation<LoginRequest>();

        group.MapPost("/refresh", async (RefreshRequest request, IAuthService auth, CancellationToken ct) =>
                ToResult(await auth.RefreshAsync(request, ct), StatusCodes.Status401Unauthorized))
            .WithValidation<RefreshRequest>();

        group.MapPost("/logout", async (LogoutRequest request, IAuthService auth, CancellationToken ct) =>
            {
                await auth.LogoutAsync(request, ct);
                return Results.NoContent();
            })
            .WithValidation<LogoutRequest>();
    }

    private static IResult ToResult(AuthOutcome outcome, int failureStatusCode) =>
        outcome.Succeeded
            ? Results.Ok(outcome.Response)
            : Results.Problem(title: outcome.Error, statusCode: failureStatusCode);
}
