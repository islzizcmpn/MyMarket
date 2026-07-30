using System.Net;
using System.Text;
using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;
using PcMarket.Mobile.Core;

namespace PcMarket.UnitTests.Mobile;

/// <summary>The guard is what stops an expired token from turning into a visible failure. The behaviour that
/// matters: refresh before a call that would fail, retry once when the server rejects a token anyway, refresh
/// only once when several screens race, and sign out cleanly when the refresh token is gone.</summary>
public class SessionGuardTests
{
    private const string InitialAccessToken = "access-1";

    [Fact]
    public async Task ExpiredToken_IsRefreshedBeforeTheCall()
    {
        var (guard, session, handler, _) = await BuildAsync(expiresInSeconds: -60);

        var tokenSeenByCall = await guard.ExecuteAsync(_ => Task.FromResult(session.AccessToken));

        Assert.Equal(1, handler.RefreshCount);
        Assert.Equal("access-2", tokenSeenByCall);
    }

    [Fact]
    public async Task ValidToken_IsNotRefreshed()
    {
        var (guard, _, handler, _) = await BuildAsync(expiresInSeconds: 900);

        await guard.ExecuteAsync(_ => Task.FromResult(0));

        Assert.Equal(0, handler.RefreshCount);
    }

    [Fact]
    public async Task Unauthorized_RefreshesAndRetriesOnce()
    {
        var (guard, _, handler, _) = await BuildAsync(expiresInSeconds: 900);
        var attempts = 0;

        var result = await guard.ExecuteAsync(_ =>
        {
            attempts++;
            // The server rejects the first attempt even though the token looks fresh to us.
            return attempts == 1
                ? throw new ApiException(HttpStatusCode.Unauthorized, "Unauthorized")
                : Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
        Assert.Equal(1, handler.RefreshCount);
    }

    [Fact]
    public async Task ConcurrentCalls_RefreshTheTokenOnlyOnce()
    {
        var (guard, _, handler, _) = await BuildAsync(expiresInSeconds: -60);

        // Four screens loading at once must not each rotate the refresh token — rotation revokes the
        // previous one, so racing refreshes would sign the user out.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => guard.ExecuteAsync(_ => Task.FromResult(0))));

        Assert.Equal(1, handler.RefreshCount);
    }

    [Fact]
    public async Task RejectedRefresh_SignsOut_AndSurfacesTheOriginalFailure()
    {
        var (guard, session, handler, _) = await BuildAsync(expiresInSeconds: 900);
        handler.RefreshSucceeds = false;

        var signedOut = false;
        guard.SignedOut += () => signedOut = true;

        await Assert.ThrowsAsync<ApiException>(() => guard.ExecuteAsync<string>(
            _ => throw new ApiException(HttpStatusCode.Unauthorized, "Unauthorized")));

        Assert.True(signedOut);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task NonAuthFailures_AreNotRetried()
    {
        var (guard, _, handler, _) = await BuildAsync(expiresInSeconds: 900);
        var attempts = 0;

        await Assert.ThrowsAsync<ApiException>(() => guard.ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new ApiException(HttpStatusCode.BadRequest, "Only 2 item(s) in stock.");
        }));

        Assert.Equal(1, attempts);
        Assert.Equal(0, handler.RefreshCount);
    }

    private static async Task<(SessionGuard Guard, MobileSession Session, RefreshHandler Handler, InMemorySessionStorage Storage)>
        BuildAsync(int expiresInSeconds)
    {
        var storage = new InMemorySessionStorage();
        var session = new MobileSession(storage);
        await session.LoadAsync();

        var expiry = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        await session.SetAuthAsync(new AuthResponse(
            InitialAccessToken, expiry, "refresh-1", expiry.AddDays(7), Guid.NewGuid(), ["Customer"]));

        var handler = new RefreshHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/v1/") };
        var auth = new AuthApiClient(http, new MobileApiTokenProvider(session));

        return (new SessionGuard(session, auth), session, handler, storage);
    }

    /// <summary>Answers <c>auth/refresh</c> with a rotated token pair, or a 401 when told to fail.</summary>
    private sealed class RefreshHandler : HttpMessageHandler
    {
        public int RefreshCount { get; private set; }

        public bool RefreshSucceeds { get; set; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.EndsWith("auth/refresh", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            RefreshCount++;

            // A real refresh is not instant; the delay gives concurrent callers time to pile up on the gate.
            await Task.Delay(20, cancellationToken);

            if (!RefreshSucceeds)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"title":"Invalid refresh token."}""", Encoding.UTF8, "application/json")
                };
            }

            var expiry = DateTimeOffset.UtcNow.AddMinutes(15);
            var json = $$"""
            {"accessToken":"access-2","accessTokenExpiresAt":"{{expiry:O}}","refreshToken":"refresh-2",
             "refreshTokenExpiresAt":"{{expiry.AddDays(7):O}}","userId":"{{Guid.NewGuid()}}","roles":["Customer"]}
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
