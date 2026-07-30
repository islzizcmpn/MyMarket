using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Auth;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PcMarket.IntegrationTests;

/// <summary>A six-digit code is 1,000,000 possibilities and lives for five minutes, which is only safe while
/// guesses are capped: an uncapped caller can simply enumerate. These cover both places a code is checked —
/// the auth API and the bot's own link flow — because they are separate implementations.</summary>
public class Phase7BotOtpAttemptTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ApiPhone = "+998907772001";
    private const long BotTelegramId = 772_002;
    private const string BotPhone = "+998907772002";

    [Fact]
    public async Task AuthApi_DiscardsTheCode_AfterTooManyWrongGuesses()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(ApiPhone, "Guessy!23456", "Guessy"));

        var realCode = await ReadCacheAsync(OtpKeys.For(ApiPhone));
        Assert.NotNull(realCode);

        for (var attempt = 0; attempt < OtpPolicy.MaxAttempts; attempt++)
        {
            var wrong = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(ApiPhone, "000000"));
            Assert.NotEqual(HttpStatusCode.OK, wrong.StatusCode);
        }

        // The code is gone, so even the genuine one no longer works — guessing must restart from a new SMS.
        Assert.Null(await ReadCacheAsync(OtpKeys.For(ApiPhone)));

        var withRealCode = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(ApiPhone, realCode!));
        Assert.NotEqual(HttpStatusCode.OK, withRealCode.StatusCode);
    }

    [Fact]
    public async Task AuthApi_AllowsTheRealCode_WhileTheBudgetHolds()
    {
        var client = factory.CreateClient();
        const string phone = "+998907772003";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(phone, "Typo!234567", "Typo"));

        var realCode = await ReadCacheAsync(OtpKeys.For(phone));

        // A customer who mistypes a few times must still be able to finish.
        for (var attempt = 0; attempt < OtpPolicy.MaxAttempts - 1; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(phone, "000000"));
        }

        var accepted = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(phone, realCode!));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task BotLinkFlow_DiscardsTheCode_AfterTooManyWrongGuesses()
    {
        // An account must already exist, so /link takes the bot's own OTP branch rather than registration.
        await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
            .RegisterVerifiedAsync(new RegisterRequest(BotPhone, "Existing!23456", "Existing")));

        await DispatchAsync(Command("/start"));
        await DispatchAsync(Button(BotCommands.Link));
        await DispatchAsync(Command(BotPhone));

        var otpKey = $"bot:otp:{BotTelegramId}";
        var realCode = await ReadCacheAsync(otpKey);
        Assert.NotNull(realCode);

        for (var attempt = 0; attempt < OtpPolicy.MaxAttempts; attempt++)
        {
            await DispatchAsync(Command("000000"));
        }

        Assert.Null(await ReadCacheAsync(otpKey));

        // Even the genuine code is now worthless, and no link was created.
        await DispatchAsync(Command(realCode!));
        Assert.Null(await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(BotTelegramId)));
    }

    private async Task DispatchAsync(Update update)
    {
        using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(update);
    }

    private static Update Command(string text) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Message = new Message
            {
                Id = Random.Shared.Next(1, int.MaxValue),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = BotTelegramId, Type = ChatType.Private },
                From = new User { Id = BotTelegramId, FirstName = "Guessy" },
                Text = text
            }
        };

    private static Update Button(string callbackData) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                ChatInstance = BotTelegramId.ToString(),
                From = new User { Id = BotTelegramId, FirstName = "Guessy" },
                Data = callbackData,
                Message = new Message
                {
                    Id = Random.Shared.Next(1, int.MaxValue),
                    Date = DateTime.UtcNow,
                    Chat = new Chat { Id = BotTelegramId, Type = ChatType.Private }
                }
            }
        };

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<string?> ReadCacheAsync(string key) =>
        WithScopeAsync(sp => sp.GetRequiredService<ICacheService>().GetAsync<string>(key));
}
