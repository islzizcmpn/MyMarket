using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Catalog;
using PcMarket.Domain.Enums;
using PcMarket.Infrastructure.Persistence;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Dto = PcMarket.Contracts.Orders;

namespace PcMarket.IntegrationTests;

/// <summary>Drives the Telegram bot end to end. Updates are fed straight to
/// <see cref="TelegramUpdateHandler"/> — the webhook endpoint's only extra job is authenticating Telegram,
/// which is covered separately — and every assertion reads the database, since the bot runs token-less here
/// and its outbound messages go nowhere.</summary>
public class Phase7BotTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const long ShopperTelegramId = 770_001;
    private const long AdminTelegramId = 770_002;
    private const string ShopperPhone = "+998907770001";
    private const string SeededAdminPhone = "+998900000000";

    [Fact]
    public async Task Webhook_RequiresTelegramSecretToken()
    {
        var client = factory.CreateClient();
        const string body = """
        {"update_id":900001,"message":{"message_id":1,"date":1750000000,
         "chat":{"id":990001,"type":"private"},
         "from":{"id":990001,"is_bot":false,"first_name":"Secretless"},"text":"/help"}}
        """;

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhookAsync(client, body, secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhookAsync(client, body, "not-the-secret")).StatusCode);

        var accepted = await PostWebhookAsync(client, body, ApiFactory.WebhookSecret);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Bot_Browse_Link_Checkout_AndAdminAdvance()
    {
        var client = factory.CreateClient();
        var product = await client.GetFromJsonAsync<ProductDetailDto>("/api/v1/catalog/products/asus-vivobook-15");
        var variantId = product!.Variants[0].Id;

        // 1. A brand-new chat gets the menu, then adds a product to a guest cart keyed by Telegram id.
        await DispatchAsync(Command("/start"));
        await DispatchAsync(Button(CallbackData.Of(BotCommands.AddToCart, variantId)));

        var guestToken = $"tg{ShopperTelegramId}";
        var guestQty = await WithDbAsync(async db =>
            (await db.Carts.Include(c => c.Items).SingleAsync(c => c.Token == guestToken)).Items.Single().Qty);
        Assert.Equal(1, guestQty);

        // 2. Linking an unknown phone registers the account; verifying the OTP links it and merges the cart.
        await DispatchAsync(Button(BotCommands.Link));
        await DispatchAsync(Command(ShopperPhone));

        var otp = await ReadCacheAsync(OtpKeys.For(ShopperPhone));
        Assert.NotNull(otp);
        await DispatchAsync(Command(otp!));

        var link = await WithScopeAsync(sp =>
            sp.GetRequiredService<ITelegramLinkStore>().FindByTelegramUserIdAsync(ShopperTelegramId));
        Assert.NotNull(link);
        Assert.Equal(ShopperPhone, link!.Phone);
        var userId = link.UserId;

        Assert.False(await WithDbAsync(db => db.Carts.AnyAsync(c => c.Token == guestToken)));
        var mergedVariantId = await WithDbAsync(async db =>
            (await db.Carts.Include(c => c.Items).SingleAsync(c => c.UserId == userId)).Items.Single().ProductVariantId);
        Assert.Equal(variantId, mergedVariantId);

        // 3. Checkout: a shared location pin, then the house and flat as free text, then cash on delivery.
        await DispatchAsync(Button(BotCommands.Checkout));
        await DispatchAsync(Pin(41.311081, 69.240562));
        await DispatchAsync(Command("12, flat 5"));
        await DispatchAsync(Button(CallbackData.Of(BotCommands.PaymentMethod, (int)Dto.PaymentMethod.Cash)));

        var order = await WithDbAsync(db => db.Orders.Include(o => o.Items).SingleAsync(o => o.UserId == userId));
        // Cash on delivery skips AwaitingPayment and lands straight in Processing.
        Assert.Equal(OrderStatus.Processing, order.Status);
        // The pin is the address for a bot order: it is what the courier navigates to, so it has to survive
        // the round trip into the order's JSON address snapshot.
        Assert.Equal(41.311081, order.ShippingAddress.Latitude);
        Assert.Equal(69.240562, order.ShippingAddress.Longitude);
        Assert.Equal("12, flat 5", order.ShippingAddress.Street);
        Assert.Single(order.Items);

        // 4. The customer's own linked account must not be able to drive admin transitions.
        var shipped = CallbackData.Of(BotCommands.AdminAdvance, order.Id, (int)Dto.OrderStatus.Shipped);
        await DispatchAsync(Button(shipped));
        Assert.Equal(OrderStatus.Processing, await StatusOfAsync(order.Id));

        // 5. A manager whose Telegram account is linked to an Admin user can advance it from the chat.
        await WithScopeAsync(async sp =>
        {
            var admin = await sp.GetRequiredService<IUserDirectory>().FindByPhoneAsync(SeededAdminPhone);
            Assert.NotNull(admin);
            await sp.GetRequiredService<ITelegramLinkStore>().LinkAsync(admin!.Id, AdminTelegramId);
            return true;
        });

        await DispatchAsync(Button(shipped, AdminTelegramId));

        var advanced = await WithDbAsync(db => db.Orders.Include(o => o.StatusHistory).SingleAsync(o => o.Id == order.Id));
        Assert.Equal(OrderStatus.Shipped, advanced.Status);
        Assert.Contains(advanced.StatusHistory, h => h.ToStatus == OrderStatus.Shipped);
    }

    private static Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string body, string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bot/telegram/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }

        return client.SendAsync(request);
    }

    private async Task DispatchAsync(Update update)
    {
        // One scope per update, matching how the webhook endpoint resolves the handler per request.
        using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(update);
    }

    private static Update Command(string text, long telegramUserId = ShopperTelegramId) =>
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

    /// <summary>A shared location, the way Telegram delivers one when the customer taps the request-location
    /// button.</summary>
    private static Update Pin(double latitude, double longitude, long telegramUserId = ShopperTelegramId) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Message = new Message
            {
                Id = Random.Shared.Next(1, int.MaxValue),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = telegramUserId, Type = ChatType.Private },
                From = new User { Id = telegramUserId, FirstName = "Tester" },
                Location = new Location { Latitude = latitude, Longitude = longitude }
            }
        };

    private static Update Button(string callbackData, long telegramUserId = ShopperTelegramId) =>
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

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<T> WithDbAsync<T>(Func<PcMarketDbContext, Task<T>> action) =>
        WithScopeAsync(sp => action(sp.GetRequiredService<PcMarketDbContext>()));

    private Task<OrderStatus> StatusOfAsync(Guid orderId) =>
        WithDbAsync(async db => (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status);

    private Task<string?> ReadCacheAsync(string key) =>
        WithScopeAsync(sp => sp.GetRequiredService<ICacheService>().GetAsync<string>(key));
}
