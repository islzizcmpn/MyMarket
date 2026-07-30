using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Auth;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PcMarket.IntegrationTests;

/// <summary>Linking by shared contact. Telegram lets a user share <em>any</em> card from their address book, so
/// the phone number on a shared contact is only trustworthy when the card's <c>UserId</c> is the sender's own —
/// that is the number Telegram verified at signup. Trusting a card without that check would let anyone link
/// themselves to a stranger's account by forwarding the stranger's contact card, which is why these two cases
/// are tested together.</summary>
public class Phase7BotContactLinkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const long OwnerTelegramId = 771_101;
    private const long AttackerTelegramId = 771_102;
    private const string OwnerPhone = "+998907771101";
    private const string VictimPhone = "+998907771102";

    [Fact]
    public async Task SharingYourOwnContact_LinksImmediately_WithoutAnyOtp()
    {
        await DispatchAsync(Command("/start", OwnerTelegramId));
        await DispatchAsync(Contact(OwnerPhone, contactUserId: OwnerTelegramId, senderId: OwnerTelegramId));

        var link = await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(OwnerTelegramId));

        Assert.NotNull(link);
        Assert.Equal(OwnerPhone, link!.Phone);

        // No SMS was worth sending, so no code should have been issued for this number.
        Assert.Null(await ReadCacheAsync(OtpKeys.For(OwnerPhone)));
        Assert.Null(await ReadCacheAsync($"bot:otp:{OwnerTelegramId}"));
    }

    [Fact]
    public async Task SharingSomeoneElsesContact_CannotHijackTheirExistingAccount()
    {
        // The victim already has a real, confirmed account — this is what makes the attack worth attempting.
        var victimId = await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
            .RegisterVerifiedAsync(new RegisterRequest(VictimPhone, "Victim!23456", "Victim")));

        await DispatchAsync(Command("/start", AttackerTelegramId));

        // The victim's card, forwarded from the attacker's address book: a real phone number and a real Telegram
        // UserId — just not the sender's. Trusting it would hand the attacker the victim's orders and addresses.
        await DispatchAsync(Contact(VictimPhone, contactUserId: 999_999, senderId: AttackerTelegramId));

        Assert.Null(await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(AttackerTelegramId)));

        // And from the other direction: the victim's account must not have acquired the attacker's Telegram id.
        var victimsTelegramId = await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().GetTelegramUserIdAsync(victimId));
        Assert.NotEqual(AttackerTelegramId, victimsTelegramId);
    }

    [Fact]
    public async Task SharingAContactWithNoTelegramAccount_DoesNotLink()
    {
        const long stranger = 771_103;
        await DispatchAsync(Command("/start", stranger));

        // A card for a plain phonebook entry: UserId is absent entirely.
        await DispatchAsync(Contact("+998907771103", contactUserId: null, senderId: stranger));

        Assert.Null(await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(stranger)));
    }

    private async Task DispatchAsync(Update update)
    {
        using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(update);
    }

    private static Update Command(string text, long telegramUserId) =>
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

    private static Update Contact(string phone, long? contactUserId, long senderId) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Message = new Message
            {
                Id = Random.Shared.Next(1, int.MaxValue),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = senderId, Type = ChatType.Private },
                From = new User { Id = senderId, FirstName = "Tester" },
                Contact = new Contact
                {
                    PhoneNumber = phone,
                    FirstName = "Shared",
                    UserId = contactUserId
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
