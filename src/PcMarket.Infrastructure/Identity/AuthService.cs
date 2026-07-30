using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Messaging;
using PcMarket.Contracts.Auth;
using PcMarket.Domain.Common;

namespace PcMarket.Infrastructure.Identity;

/// <summary>Phone-first authentication backed by ASP.NET Core Identity. OTP codes live in the cache with
/// a short TTL; refresh tokens are rotated on every use.</summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenStore refreshTokenStore,
    ISmsSender smsSender,
    ICacheService cache) : IAuthService
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.Phone.Trim();
        var existing = await userManager.FindByNameAsync(phone);

        if (existing is { PhoneNumberConfirmed: true })
        {
            throw new DomainException("Phone number is already registered.");
        }

        if (existing is null)
        {
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = phone,
                PhoneNumber = phone,
                FullName = request.FullName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, Roles.Customer);
        }

        await IssueOtpAsync(phone, cancellationToken);

        return new RegisterResponse(phone, true);
    }

    public async Task<Guid> RegisterVerifiedAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.Phone.Trim();
        var user = await userManager.FindByNameAsync(phone);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = phone,
                PhoneNumber = phone,
                FullName = request.FullName,
                // Proven by the caller, not by us — see IAuthService.RegisterVerifiedAsync.
                PhoneNumberConfirmed = true
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, Roles.Customer);
            return user.Id;
        }

        // An account registered earlier but never verified is confirmed now: we just received better evidence
        // than the OTP it was waiting for. An already-confirmed account is returned untouched — in particular
        // its FullName is not overwritten by whatever the chat happens to be called.
        if (!user.PhoneNumberConfirmed)
        {
            user.PhoneNumberConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return user.Id;
    }

    public async Task<AuthOutcome> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = request.Phone.Trim();
        var expected = await cache.GetAsync<string>(OtpKeys.For(phone), cancellationToken);
        if (expected is null || expected != request.Code)
        {
            // A wrong guess costs the caller one of a small budget; exhausting it discards the code entirely.
            // The message stays identical either way, so it never reveals whether the code merely expired,
            // was wrong, or has just been burned.
            if (expected is not null)
            {
                await CountFailedOtpAttemptAsync(phone, cancellationToken);
            }

            return AuthOutcome.Fail("Invalid or expired verification code.");
        }

        var user = await userManager.FindByNameAsync(phone);
        if (user is null)
        {
            return AuthOutcome.Fail("User not found.");
        }

        user.PhoneNumberConfirmed = true;
        await userManager.UpdateAsync(user);
        await ClearOtpAsync(phone, cancellationToken);

        return AuthOutcome.Ok(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.Phone.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return AuthOutcome.Fail("Invalid phone or password.");
        }

        if (!user.PhoneNumberConfirmed)
        {
            return AuthOutcome.Fail("Phone number is not verified.");
        }

        return AuthOutcome.Ok(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<AuthOutcome> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await refreshTokenStore.ValidateAsync(request.RefreshToken, cancellationToken);
        if (!validation.IsValid)
        {
            return AuthOutcome.Fail("Invalid or expired refresh token.");
        }

        var user = await userManager.FindByIdAsync(validation.UserId.ToString());
        if (user is null)
        {
            return AuthOutcome.Fail("User not found.");
        }

        // Rotate: the presented token is revoked and a fresh pair is issued.
        await refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
        return AuthOutcome.Ok(await IssueTokensAsync(user, cancellationToken));
    }

    public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) =>
        refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);

    /// <summary>Issues a fresh code and resets the guess budget. Requesting a new code is the only way to get
    /// more attempts — and that path sends an SMS and is rate limited, which is what bounds guessing overall.</summary>
    private async Task IssueOtpAsync(string phone, CancellationToken cancellationToken)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await cache.SetAsync(OtpKeys.For(phone), code, OtpPolicy.Ttl, cancellationToken);
        await cache.RemoveAsync(OtpKeys.AttemptsFor(phone), cancellationToken);
        await smsSender.SendAsync(phone, $"PcMarket verification code: {code}", cancellationToken);
    }

    private async Task CountFailedOtpAttemptAsync(string phone, CancellationToken cancellationToken)
    {
        var key = OtpKeys.AttemptsFor(phone);
        var attempts = int.TryParse(await cache.GetAsync<string>(key, cancellationToken), out var parsed) ? parsed : 0;
        attempts++;

        if (attempts >= OtpPolicy.MaxAttempts)
        {
            await ClearOtpAsync(phone, cancellationToken);
            return;
        }

        // Rides the code's own TTL: once the code is gone the counter is meaningless.
        await cache.SetAsync(key, attempts.ToString(), OtpPolicy.Ttl, cancellationToken);
    }

    private async Task ClearOtpAsync(string phone, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(OtpKeys.For(phone), cancellationToken);
        await cache.RemoveAsync(OtpKeys.AttemptsFor(phone), cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var access = tokenService.IssueAccessToken(new TokenUser(user.Id, user.UserName ?? string.Empty, roles));
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.Add(tokenService.RefreshTokenLifetime);

        await refreshTokenStore.StoreAsync(user.Id, refreshToken, refreshExpiresAt, cancellationToken);

        return new AuthResponse(
            access.Value,
            access.ExpiresAt,
            refreshToken,
            refreshExpiresAt,
            user.Id,
            roles.ToList());
    }
}
