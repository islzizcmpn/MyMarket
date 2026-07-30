using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Contracts.Auth;
using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Orders;
using PcMarket.Contracts.Payments;
using PcMarket.Infrastructure.Persistence;
using PcMarket.Payments.Configuration;

namespace PcMarket.IntegrationTests;

/// <summary>Drives full checkout → gateway-webhook flows for Click and Payme against the real API + Postgres,
/// asserting the order reaches Paid and that replaying a webhook does not double-apply (idempotency).</summary>
public class Phase4PaymentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // The API serialises enums as names; mirror that when (de)serialising order/payment DTOs here.
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Click_PrepareThenComplete_DrivesOrderToPaid_AndReplayIsIdempotent()
    {
        var shopper = factory.CreateClient();
        await AuthenticateAsync(shopper, "+998901000001");
        var order = await CreateOrderAsync(shopper, PaymentMethod.Click);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);

        // The initiate endpoint returns a redirect URL for the online rail.
        var initiate = await shopper.PostAsJsonAsync("/api/v1/payments/initiate", new PaymentInitiateRequest(order.Id), Json);
        initiate.EnsureSuccessStatusCode();
        var initiation = await initiate.Content.ReadFromJsonAsync<PaymentInitiationResponse>(Json);
        Assert.True(initiation!.RequiresRedirect);
        Assert.False(string.IsNullOrWhiteSpace(initiation.PaymentUrl));

        var click = factory.CreateClient();
        var settings = Options<PaymentsSettings>().Click;
        const string clickTransId = "100001";
        var amount = order.Total.ToString("0.##", CultureInfo.InvariantCulture);
        const string signTime = "2026-07-25 10:00:00";

        var prepareSign = Md5(clickTransId + settings.ServiceId + settings.SecretKey + order.Number + amount + "0" + signTime);
        var prepare = await PostClickAsync(click, new Dictionary<string, string>
        {
            ["click_trans_id"] = clickTransId,
            ["service_id"] = settings.ServiceId,
            ["click_paydoc_id"] = "500001",
            ["merchant_trans_id"] = order.Number,
            ["amount"] = amount,
            ["action"] = "0",
            ["error"] = "0",
            ["sign_time"] = signTime,
            ["sign_string"] = prepareSign
        });
        Assert.Equal(0, prepare.GetProperty("error").GetInt32());
        Assert.Equal(clickTransId, prepare.GetProperty("merchant_prepare_id").GetString());

        var completeSign = Md5(clickTransId + settings.ServiceId + settings.SecretKey + order.Number + clickTransId + amount + "1" + signTime);
        var completeForm = new Dictionary<string, string>
        {
            ["click_trans_id"] = clickTransId,
            ["service_id"] = settings.ServiceId,
            ["click_paydoc_id"] = "500001",
            ["merchant_trans_id"] = order.Number,
            ["merchant_prepare_id"] = clickTransId,
            ["amount"] = amount,
            ["action"] = "1",
            ["error"] = "0",
            ["sign_time"] = signTime,
            ["sign_string"] = completeSign
        };

        var complete = await PostClickAsync(click, completeForm);
        Assert.Equal(0, complete.GetProperty("error").GetInt32());
        Assert.Equal(OrderStatus.Paid, (await GetOrderAsync(shopper, order.Id)).Status);

        // Replay the Complete callback — still success, still exactly one settled ledger entry.
        var replay = await PostClickAsync(click, completeForm);
        Assert.Equal(0, replay.GetProperty("error").GetInt32());
        Assert.Equal(OrderStatus.Paid, (await GetOrderAsync(shopper, order.Id)).Status);
        Assert.Equal(1, await CountPerformedAsync(order.Id));
    }

    [Fact]
    public async Task Payme_CreateThenPerform_DrivesOrderToPaid_AndReplayIsIdempotent()
    {
        var shopper = factory.CreateClient();
        await AuthenticateAsync(shopper, "+998901000002");
        var order = await CreateOrderAsync(shopper, PaymentMethod.Payme);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);

        var merchantKey = Options<PaymentsSettings>().Payme.MerchantKey;
        var gateway = factory.CreateClient();
        const string paymeTxnId = "payme-txn-0001";
        var tiyin = (long)(order.Total * 100);

        var create = await PostPaymeAsync(gateway, merchantKey, "CreateTransaction", new
        {
            id = paymeTxnId,
            time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            amount = tiyin,
            account = new { order_id = order.Number }
        });
        Assert.Equal(1, create.GetProperty("result").GetProperty("state").GetInt32());

        var perform = await PostPaymeAsync(gateway, merchantKey, "PerformTransaction", new { id = paymeTxnId });
        Assert.Equal(2, perform.GetProperty("result").GetProperty("state").GetInt32());
        Assert.Equal(OrderStatus.Paid, (await GetOrderAsync(shopper, order.Id)).Status);

        // Replay PerformTransaction — Payme's contract requires the same performed result, once.
        var replay = await PostPaymeAsync(gateway, merchantKey, "PerformTransaction", new { id = paymeTxnId });
        Assert.Equal(2, replay.GetProperty("result").GetProperty("state").GetInt32());
        Assert.Equal(1, await CountPerformedAsync(order.Id));
    }

    private async Task<OrderDto> CreateOrderAsync(HttpClient client, PaymentMethod method)
    {
        var variantId = await GetFirstVariantIdAsync(client);
        (await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(variantId, 1))).EnsureSuccessStatusCode();

        var request = new CreateOrderRequest(
            method,
            DeliveryType.Courier,
            AddressId: null,
            Address: new ShippingAddressDto("Toshkent shahri", "Tashkent", "Amir Temur 1", null));

        var response = await client.PostAsJsonAsync("/api/v1/orders", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(Json))!;
    }

    private static async Task<OrderDto> GetOrderAsync(HttpClient client, Guid orderId) =>
        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{orderId}", Json))!;

    private async Task AuthenticateAsync(HttpClient client, string phone)
    {
        (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(phone, "Passw0rd!", "Buyer")))
            .EnsureSuccessStatusCode();

        using var scope = factory.CreateScope();
        var code = await scope.ServiceProvider.GetRequiredService<ICacheService>().GetAsync<string>(OtpKeys.For(phone));

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(phone, code!));
        verify.EnsureSuccessStatusCode();
        var tokens = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task<Guid> GetFirstVariantIdAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<ProductDetailDto>("/api/v1/catalog/products/asus-vivobook-15");
        return detail!.Variants[0].Id;
    }

    private async Task<int> CountPerformedAsync(Guid orderId)
    {
        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PcMarketDbContext>();
        return db.PaymentTransactions.Count(t =>
            t.OrderId == orderId && t.State == PcMarket.Domain.Enums.PaymentTransactionState.Performed);
    }

    private T Options<T>() where T : class, new()
    {
        using var scope = factory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<T>>().Value;
    }

    private static async Task<JsonElement> PostClickAsync(HttpClient client, Dictionary<string, string> form)
    {
        var response = await client.PostAsync("/api/v1/payments/click/callback", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> PostPaymeAsync(HttpClient client, string merchantKey, string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/payme/callback")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["method"] = method,
                ["params"] = parameters,
                ["id"] = 1
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"Paycom:{merchantKey}")));

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string Md5(string input) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(input)));
}
