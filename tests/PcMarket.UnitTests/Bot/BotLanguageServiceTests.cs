using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Localization;

namespace PcMarket.UnitTests.Bot;

/// <summary>Where a chat's language comes from, in order: what the account says, then what the guest chose
/// before they had an account, then the language their Telegram app is in, and finally Russian.</summary>
public class BotLanguageServiceTests
{
    [Fact]
    public async Task Resolve_PrefersTheAccountsLanguage()
    {
        var service = Service(accountLanguage: "en", guestLanguage: "uz");

        Assert.Equal("en", await service.ResolveAsync(TelegramUserId, "uz"));
    }

    [Fact]
    public async Task Resolve_UsesTheGuestChoice_WhenNoAccountCarriesOne()
    {
        var service = Service(accountLanguage: null, guestLanguage: "uz");

        Assert.Equal("uz", await service.ResolveAsync(TelegramUserId, "en"));
    }

    [Theory]
    [InlineData("uz", "uz")]
    [InlineData("en-GB", "en")]
    [InlineData("ru-RU", "ru")]
    public async Task Resolve_FallsBackToTelegramsOwnLanguage_OnFirstContact(string telegramCode, string expected)
    {
        var service = Service(accountLanguage: null, guestLanguage: null);

        Assert.Equal(expected, await service.ResolveAsync(TelegramUserId, telegramCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fr")]
    public async Task Resolve_FallsBackToRussian_WhenNothingUsableIsKnown(string? telegramCode)
    {
        var service = Service(accountLanguage: null, guestLanguage: null);

        Assert.Equal("ru", await service.ResolveAsync(TelegramUserId, telegramCode));
    }

    [Fact]
    public async Task Set_WritesToTheAccount_WhenOneIsLinked()
    {
        var users = new FakeUserLanguageStore(null);
        var conversations = new FakeConversationStore(null);
        var service = new BotLanguageService(new FakeLinkStore(LinkedUserId), users, conversations);

        await service.SetAsync(TelegramUserId, "uz");

        Assert.Equal("uz", users.Languages[LinkedUserId]);
        Assert.Equal("uz", conversations.Language);
    }

    [Fact]
    public async Task Set_KeepsAGuestsChoiceInTheConversationStore()
    {
        var users = new FakeUserLanguageStore(null);
        var conversations = new FakeConversationStore(null);
        var service = new BotLanguageService(new FakeLinkStore(null), users, conversations);

        await service.SetAsync(TelegramUserId, "en");

        Assert.Empty(users.Languages);
        Assert.Equal("en", conversations.Language);
    }

    [Fact]
    public async Task Adopt_DoesNotOverwriteALanguageTheAccountAlreadyHas()
    {
        var users = new FakeUserLanguageStore("en");
        var service = new BotLanguageService(new FakeLinkStore(LinkedUserId), users, new FakeConversationStore(null));

        await service.AdoptAsync(LinkedUserId, "uz");

        Assert.Empty(users.Languages);
    }

    [Fact]
    public async Task Adopt_CarriesTheGuestChoiceOntoTheNewAccount()
    {
        var users = new FakeUserLanguageStore(null);
        var service = new BotLanguageService(new FakeLinkStore(LinkedUserId), users, new FakeConversationStore(null));

        await service.AdoptAsync(LinkedUserId, "uz");

        Assert.Equal("uz", users.Languages[LinkedUserId]);
    }

    private const long TelegramUserId = 4242;

    private static readonly Guid LinkedUserId = Guid.NewGuid();

    private static BotLanguageService Service(string? accountLanguage, string? guestLanguage) =>
        new(new FakeLinkStore(accountLanguage is null ? null : LinkedUserId),
            new FakeUserLanguageStore(accountLanguage),
            new FakeConversationStore(guestLanguage));

    private sealed class FakeUserLanguageStore(string? stored) : IUserLanguageStore
    {
        public Dictionary<Guid, string> Languages { get; } = [];

        public Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(stored);

        public Task<string?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(stored);

        public Task SetAsync(Guid userId, string culture, CancellationToken cancellationToken = default)
        {
            Languages[userId] = culture;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLinkStore(Guid? userId) : ITelegramLinkStore
    {
        public Task<TelegramLink?> FindByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId is { } id ? new TelegramLink(id, telegramUserId, "+998900000000", null, []) : null);

        public Task<long?> GetTelegramUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(TelegramUserId);

        public Task LinkAsync(Guid userId, long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> UnlinkAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeConversationStore(string? language) : IConversationStore
    {
        public string? Language { get; private set; } = language;

        public Task<string?> GetLanguageAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Language);

        public Task SetLanguageAsync(long telegramUserId, string culture, CancellationToken cancellationToken = default)
        {
            Language = culture;
            return Task.CompletedTask;
        }

        public Task<ConversationState> GetAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConversationState.Empty);

        public Task SetAsync(long telegramUserId, ConversationState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(long telegramUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetOtpAsync(long telegramUserId, string code, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetOtpAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task ClearOtpAsync(long telegramUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CountFailedOtpAttemptAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
