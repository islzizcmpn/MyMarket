using PcMarket.Contracts.Auth;
using PcMarket.Mobile.Core;

namespace PcMarket.UnitTests.Mobile;

/// <summary>The mobile session is what keeps a customer signed in between launches and keeps a guest's cart
/// attached to them. Both survive a restart; a keystore we cannot read must degrade to "signed out" rather
/// than take the app down.</summary>
public class MobileSessionTests
{
    private static AuthResponse Auth(DateTimeOffset expiresAt) =>
        new("access-token", expiresAt, "refresh-token", expiresAt.AddDays(7), Guid.NewGuid(), ["Customer"]);

    [Fact]
    public async Task SignedInSession_SurvivesARestart()
    {
        var storage = new InMemorySessionStorage();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(15);

        var first = new MobileSession(storage);
        await first.LoadAsync();
        await first.SetAuthAsync(Auth(expiry));

        // A new instance stands in for the next app launch, reading the same device storage.
        var second = new MobileSession(storage);
        await second.LoadAsync();

        Assert.True(second.IsAuthenticated);
        Assert.Equal("access-token", second.AccessToken);
        Assert.Equal("refresh-token", second.RefreshToken);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(["Customer"], second.Roles);
    }

    [Fact]
    public async Task GuestCartToken_SurvivesARestart_AndClearsWhenDropped()
    {
        var storage = new InMemorySessionStorage();

        var first = new MobileSession(storage);
        await first.LoadAsync();
        await first.SetCartTokenAsync("guest-cart-42");

        var second = new MobileSession(storage);
        await second.LoadAsync();
        Assert.Equal("guest-cart-42", second.CartToken);

        // Merging into an account drops the guest token for good.
        await second.SetCartTokenAsync(null);

        var third = new MobileSession(storage);
        await third.LoadAsync();
        Assert.Null(third.CartToken);
    }

    [Fact]
    public async Task SignOut_ClearsCredentialsFromStorage()
    {
        var storage = new InMemorySessionStorage();
        var session = new MobileSession(storage);
        await session.LoadAsync();
        await session.SetAuthAsync(Auth(DateTimeOffset.UtcNow.AddMinutes(15)));

        await session.SignOutAsync();

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AccessToken);
        Assert.Null(session.RefreshToken);
        Assert.Empty(session.Roles);
        Assert.False(storage.HasStoredSession);
    }

    [Fact]
    public async Task CorruptStoredSession_LoadsAsSignedOut_AndDiscardsTheBadData()
    {
        var storage = new InMemorySessionStorage();
        storage.CorruptSession();

        var session = new MobileSession(storage);
        await session.LoadAsync();

        Assert.True(session.Loaded);
        Assert.False(session.IsAuthenticated);
        Assert.False(storage.HasStoredSession);
    }

    [Fact]
    public async Task UnreadableKeystore_LoadsAsSignedOut()
    {
        var storage = new InMemorySessionStorage { FailSecureReads = true };
        var session = new MobileSession(storage);

        await session.LoadAsync();

        Assert.True(session.Loaded);
        Assert.False(session.IsAuthenticated);
    }

    [Theory]
    // Comfortably valid.
    [InlineData(600, false)]
    // Inside the leeway window: treat as stale so a request does not go out with a token about to expire.
    [InlineData(10, true)]
    [InlineData(0, true)]
    // Already expired.
    [InlineData(-60, true)]
    public async Task NeedsRefresh_AccountsForTheExpiryLeeway(int secondsUntilExpiry, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new MobileSession(new InMemorySessionStorage());
        await session.LoadAsync();
        await session.SetAuthAsync(Auth(now.AddSeconds(secondsUntilExpiry)));

        Assert.Equal(expected, session.NeedsRefresh(now));
    }

    [Fact]
    public async Task NeedsRefresh_IsFalseWhenSignedOut()
    {
        var session = new MobileSession(new InMemorySessionStorage());
        await session.LoadAsync();

        Assert.False(session.NeedsRefresh(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task LoadAsync_HydratesOnlyOnce_EvenWhenCalledConcurrently()
    {
        var storage = new InMemorySessionStorage();
        var seed = new MobileSession(storage);
        await seed.LoadAsync();
        await seed.SetAuthAsync(Auth(DateTimeOffset.UtcNow.AddMinutes(15)));

        // Several API calls can race to hydrate the session on the first request after launch.
        var session = new MobileSession(storage);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => session.LoadAsync()));

        Assert.True(session.IsAuthenticated);
        Assert.Equal("access-token", session.AccessToken);
    }
}
