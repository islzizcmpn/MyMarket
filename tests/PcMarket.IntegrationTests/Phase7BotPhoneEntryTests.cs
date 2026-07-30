using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Auth;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PcMarket.IntegrationTests;

/// <summary>The shipped default: <c>Telegram:AllowPhoneEntry</c> is off, so the bot links only by shared
/// contact. Typing a number must not fall back to an OTP — that is the one route in the bot that spends an
/// SMS, and with no provider funded it would strand the customer on a code that never arrives.
///
/// The contact-share route must keep working untouched, which is what makes disabling this safe rather than a
/// loss of function.</summary>
public class Phase7BotPhoneEntryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const long TelegramId = 773_001;
    private const string Phone = "+998907773001";

    [Fact]
    public async Task TypingAPhoneNumber_IssuesNoCode_AndDoesNotLink()
    {
        // The account exists, so this would otherwise take the bot's own OTP branch and send an SMS.
        await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
            .RegisterVerifiedAsync(new RegisterRequest(Phone, "Existing!23456", "Existing")));

        await DispatchAsync(Command("/start"));
        await DispatchAsync(Button(BotCommands.Link));
        await DispatchAsync(Command(Phone));

        Assert.Null(await ReadCacheAsync($"bot:otp:{TelegramId}"));
        Assert.Null(await ReadCacheAsync(OtpKeys.For(Phone)));
        Assert.Null(await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(TelegramId)));
    }

    [Fact]
    public async Task SharingTheContact_StillLinks_WithPhoneEntryDisabled()
    {
        const long telegramId = 773_002;
        const string phone = "+998907773002";

        await DispatchAsync(Command("/start", telegramId));
        await DispatchAsync(Contact(phone, telegramId));

        var link = await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(telegramId));

        Assert.NotNull(link);
        Assert.Equal(phone, link!.Phone);
        Assert.Null(await ReadCacheAsync(OtpKeys.For(phone)));
    }

    /// <summary>Overrides the factory's opt-in back to the shipped default. In-memory configuration is added
    /// after the environment variables, so it wins.</summary>
    private WebApplicationFactoryScope Scoped() => new(factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:AllowPhoneEntry"] = "false"
            }))));

    private async Task DispatchAsync(Update update)
    {
        using var host = Scoped();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(update);
    }

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var host = Scoped();
        using var scope = host.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<string?> ReadCacheAsync(string key) =>
        WithScopeAsync(sp => sp.GetRequiredService<ICacheService>().GetAsync<string>(key));

    private static Update Command(string text, long telegramUserId = TelegramId) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Message = new Message
            {
                Id = Random.Shared.Next(1, int.MaxValue),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = telegramUserId, Type = ChatType.Private },
                From = new User { Id = telegramUserId, FirstName = "Tester" },
                Text = text
            }
        };

    private static Update Contact(string phone, long telegramUserId) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Message = new Message
            {
                Id = Random.Shared.Next(1, int.MaxValue),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = telegramUserId, Type = ChatType.Private },
                From = new User { Id = telegramUserId, FirstName = "Tester" },
                Contact = new Contact { PhoneNumber = phone, FirstName = "Tester", UserId = telegramUserId }
            }
        };

    private static Update Button(string callbackData, long telegramUserId = TelegramId) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            CallbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString("N"),
                ChatInstance = telegramUserId.ToString(),
                From = new User { Id = telegramUserId, FirstName = "Tester" },
                Data = callbackData,
                Message = new Message
                {
                    Id = Random.Shared.Next(1, int.MaxValue),
                    Date = DateTime.UtcNow,
                    Chat = new Chat { Id = telegramUserId, Type = ChatType.Private }
                }
            }
        };

    /// <summary>Keeps the derived factory alive for the duration of a call and disposes it after.</summary>
    private sealed class WebApplicationFactoryScope(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) : IDisposable
    {
        public IServiceProvider Services { get; } = factory.Services;

        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = factory;

        public void Dispose() => _factory.Dispose();
    }
}
