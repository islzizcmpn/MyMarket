using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Contracts.Auth;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Common;

namespace PcMarket.IntegrationTests;

public class Phase3EndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Catalog_ReturnsTree_List_AndDetail()
    {
        var client = factory.CreateClient();

        var categories = await client.GetFromJsonAsync<List<CategoryNodeDto>>("/api/v1/catalog/categories");
        Assert.NotNull(categories);
        var computers = Assert.Single(categories, c => c.Slug == "computers");
        Assert.Contains(computers.Children, c => c.Slug == "laptops");

        var products = await client.GetFromJsonAsync<PagedResult<ProductListItemDto>>("/api/v1/catalog/products");
        Assert.NotNull(products);
        Assert.Contains(products.Items, p => p.Slug == "asus-vivobook-15");

        var detail = await client.GetFromJsonAsync<ProductDetailDto>("/api/v1/catalog/products/asus-vivobook-15");
        Assert.NotNull(detail);
        Assert.Equal("16GB", detail.Specs["RAM"]);
        Assert.Single(detail.Variants);
    }

    [Fact]
    public async Task Catalog_Search_FindsProductByFullText()
    {
        var client = factory.CreateClient();

        var results = await client.GetFromJsonAsync<PagedResult<ProductListItemDto>>("/api/v1/catalog/search?q=vivobook");
        Assert.NotNull(results);
        Assert.Contains(results.Items, p => p.Slug == "asus-vivobook-15");
    }

    [Fact]
    public async Task Auth_Register_Verify_Login_Refresh_Flow()
    {
        var client = factory.CreateClient();
        const string phone = "+998901112233";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(phone, "Passw0rd!", "Test User"));
        register.EnsureSuccessStatusCode();

        var code = await ReadOtpAsync(phone);
        Assert.NotNull(code);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(phone, code!));
        verify.EnsureSuccessStatusCode();
        var tokens = await verify.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(tokens);
        Assert.Contains("Customer", tokens!.Roles);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(phone, "Passw0rd!"));
        login.EnsureSuccessStatusCode();
        var loginTokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginTokens);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(loginTokens!.RefreshToken));
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(loginTokens.RefreshToken, refreshed!.RefreshToken); // rotated

        // Old refresh token is now revoked.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(loginTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Cart_Guest_AddItem_CreatesTokenAndTotals()
    {
        var client = factory.CreateClient();
        var variantId = await GetFirstVariantIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(variantId, 2));
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();

        Assert.NotNull(cart);
        Assert.False(string.IsNullOrWhiteSpace(cart!.Token));
        Assert.Equal(2, cart.TotalQty);
        Assert.Equal(15_000_000m, cart.Subtotal);

        // The same cart is retrievable via its token header.
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cart");
        getRequest.Headers.Add("X-Cart-Token", cart.Token);
        var fetched = await (await client.SendAsync(getRequest)).Content.ReadFromJsonAsync<CartDto>();
        Assert.Equal(cart.Id, fetched!.Id);
    }

    [Fact]
    public async Task Cart_ExceedingStock_Returns400()
    {
        var client = factory.CreateClient();
        var variantId = await GetFirstVariantIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(variantId, 999));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cart_MergesGuestCartOnLogin()
    {
        var client = factory.CreateClient();
        var variantId = await GetFirstVariantIdAsync(client);

        // Guest cart with one item.
        var guest = await (await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(variantId, 1)))
            .Content.ReadFromJsonAsync<CartDto>();

        // Register + verify a user to obtain an access token.
        const string phone = "+998901112244";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(phone, "Passw0rd!", null));
        var code = await ReadOtpAsync(phone);
        var tokens = await (await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest(phone, code!)))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var mergeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cart/merge?token={guest!.Token}");
        mergeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var merged = await (await client.SendAsync(mergeRequest)).Content.ReadFromJsonAsync<CartDto>();

        Assert.NotNull(merged);
        Assert.Equal(1, merged!.TotalQty);
        Assert.Null(merged.Token); // user cart has no guest token
    }

    private async Task<string?> ReadOtpAsync(string phone)
    {
        using var scope = factory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        return await cache.GetAsync<string>(OtpKeys.For(phone));
    }

    private static async Task<Guid> GetFirstVariantIdAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<ProductDetailDto>("/api/v1/catalog/products/asus-vivobook-15");
        return detail!.Variants[0].Id;
    }
}
