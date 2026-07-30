using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Contracts.Users;
using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Notifications;
using PcMarket.Infrastructure.Identity;
using PcMarket.Infrastructure.Persistence;
using ClientPlatform = PcMarket.Contracts.Users.DevicePlatform;

namespace PcMarket.IntegrationTests;

/// <summary>Covers what the mobile app added to the backend: the device-token registry the app writes to on
/// sign-in, and the push notification channel that reads it. Registration has to be idempotent because the
/// app re-registers on every launch.</summary>
public class Phase8MobileTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task DeviceTokenEndpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync(
            "/api/v1/users/me/device-tokens", new RegisterDeviceTokenRequest("anon-token", ClientPlatform.Android));
        var delete = await client.DeleteAsync("/api/v1/users/me/device-tokens/anon-token");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task RegisteringTheSameToken_Twice_KeepsExactlyOneRow()
    {
        var client = factory.CreateClient();
        var (userId, accessToken) = await CreateCustomerAsync("+998908880001");
        const string token = "fcm-token-repeat";

        await RegisterTokenAsync(client, accessToken, token);
        var afterFirst = await ReadTokenAsync(token);

        // The app re-registers on every launch; that must refresh the row, not add another.
        await RegisterTokenAsync(client, accessToken, token);
        var afterSecond = await ReadTokenAsync(token);

        var rows = await WithDbAsync(db => db.DeviceTokens.CountAsync(t => t.Token == token));

        Assert.Equal(1, rows);
        Assert.Equal(userId, afterSecond.UserId);
        Assert.True(afterSecond.LastSeenAt >= afterFirst.LastSeenAt);
        Assert.Equal(afterFirst.CreatedAt, afterSecond.CreatedAt);
    }

    [Fact]
    public async Task ATokenThatMovesToAnotherAccount_IsReassigned_NotDuplicated()
    {
        var client = factory.CreateClient();
        var (firstUserId, firstToken) = await CreateCustomerAsync("+998908880002");
        var (secondUserId, secondToken) = await CreateCustomerAsync("+998908880003");
        const string token = "fcm-token-shared-device";

        await RegisterTokenAsync(client, firstToken, token);
        await RegisterTokenAsync(client, secondToken, token);

        var rows = await WithDbAsync(db => db.DeviceTokens.CountAsync(t => t.Token == token));
        var owner = (await ReadTokenAsync(token)).UserId;

        Assert.Equal(1, rows);
        Assert.Equal(secondUserId, owner);
        Assert.NotEqual(firstUserId, owner);
    }

    [Fact]
    public async Task DeletingADeviceToken_RemovesItAndIsSafeToRepeat()
    {
        var client = factory.CreateClient();
        var (_, accessToken) = await CreateCustomerAsync("+998908880004");
        const string token = "fcm-token-signout";

        await RegisterTokenAsync(client, accessToken, token);

        var first = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/me/device-tokens/{token}", accessToken);
        var second = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/me/device-tokens/{token}", accessToken);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.DeviceTokens.CountAsync(t => t.Token == token)));
    }

    [Fact]
    public async Task PushChannel_DeliversToEveryRegisteredDevice_AndSkipsUsersWithNone()
    {
        var client = factory.CreateClient();
        var (userId, accessToken) = await CreateCustomerAsync("+998908880005");
        var (strangerId, _) = await CreateCustomerAsync("+998908880006");

        await RegisterTokenAsync(client, accessToken, "fcm-phone");
        await RegisterTokenAsync(client, accessToken, "fcm-tablet");

        using var scope = factory.CreateScope();
        var push = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationChannel>>()
            .Single(channel => channel.Channel == NotificationChannel.Push);

        var toCustomer = await push.SendAsync(Message(userId));
        var toStranger = await push.SendAsync(Message(strangerId));

        // Both report success: a user with no devices is not a delivery failure, and must not burn retries.
        Assert.True(toCustomer);
        Assert.True(toStranger);
        Assert.Equal(2, await WithDbAsync(db => db.DeviceTokens.CountAsync(t => t.UserId == userId)));
    }

    private static NotificationMessage Message(Guid userId) =>
        new(userId, NotificationType.OrderStatusChanged, "Order shipped", "Your order is on its way.",
            new Dictionary<string, string>());

    private static async Task RegisterTokenAsync(HttpClient client, string accessToken, string token)
    {
        var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/device-tokens",
            accessToken,
            JsonContent.Create(new RegisterDeviceTokenRequest(token, ClientPlatform.Android)));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    /// <summary>Creates a confirmed customer and mints an access token straight from the container. The
    /// register/verify endpoints would do the same thing, but they sit behind the `auth` rate-limit policy
    /// (10 requests per window) which this many accounts would trip — and auth is not what these tests cover.</summary>
    private async Task<(Guid UserId, string AccessToken)> CreateCustomerAsync(string phone)
    {
        using var scope = factory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = true
        };

        Assert.True((await users.CreateAsync(user, "Passw0rd!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, Roles.Customer)).Succeeded);

        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var accessToken = tokens.IssueAccessToken(new TokenUser(user.Id, phone, [Roles.Customer]));

        return (user.Id, accessToken.Value);
    }

    private async Task<DeviceToken> ReadTokenAsync(string token) =>
        await WithDbAsync(db => db.DeviceTokens.AsNoTracking().SingleAsync(t => t.Token == token));

    private async Task<T> WithDbAsync<T>(Func<PcMarketDbContext, Task<T>> query)
    {
        using var scope = factory.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<PcMarketDbContext>());
    }
}
