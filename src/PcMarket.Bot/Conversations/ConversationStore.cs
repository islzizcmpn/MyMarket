using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Identity;

namespace PcMarket.Bot.Conversations;

/// <summary>Reads/writes the per-user conversation state and the linking OTP.</summary>
public interface IConversationStore
{
    Task<ConversationState> GetAsync(long telegramUserId, CancellationToken cancellationToken = default);

    Task SetAsync(long telegramUserId, ConversationState state, CancellationToken cancellationToken = default);

    Task ClearAsync(long telegramUserId, CancellationToken cancellationToken = default);

    Task SetOtpAsync(long telegramUserId, string code, CancellationToken cancellationToken = default);

    Task<string?> GetOtpAsync(long telegramUserId, CancellationToken cancellationToken = default);

    Task ClearOtpAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>Records one wrong guess, discarding the code once <see cref="OtpPolicy.MaxAttempts"/> is
    /// reached. Without this a caller could work through all 1,000,000 six-digit codes.</summary>
    Task CountFailedOtpAttemptAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>The language a chat with no linked account chose, or null when it never chose one. Kept apart
    /// from <see cref="ConversationState"/>, which expires within the half hour: a language is a preference,
    /// not a step in a flow.</summary>
    Task<string?> GetLanguageAsync(long telegramUserId, CancellationToken cancellationToken = default);

    Task SetLanguageAsync(long telegramUserId, string culture, CancellationToken cancellationToken = default);
}

/// <summary>Redis-backed conversation state (via <see cref="ICacheService"/>). State expires on its own, so
/// an abandoned checkout simply drops back to the main menu rather than lingering forever.</summary>
public sealed class ConversationStore(ICacheService cache) : IConversationStore
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan OtpTtl = OtpPolicy.Ttl;

    /// <summary>How long a guest's language choice is remembered. Long enough that a returning shopper is not
    /// asked twice; the moment they link an account the choice moves onto the account and stops expiring.</summary>
    private static readonly TimeSpan LanguageTtl = TimeSpan.FromDays(180);

    public async Task<ConversationState> GetAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
        await cache.GetAsync<ConversationState>(StateKey(telegramUserId), cancellationToken) ?? ConversationState.Empty;

    public Task SetAsync(long telegramUserId, ConversationState state, CancellationToken cancellationToken = default) =>
        cache.SetAsync(StateKey(telegramUserId), state, StateTtl, cancellationToken);

    public Task ClearAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(StateKey(telegramUserId), cancellationToken);

    public async Task SetOtpAsync(long telegramUserId, string code, CancellationToken cancellationToken = default)
    {
        await cache.SetAsync(OtpKey(telegramUserId), code, OtpTtl, cancellationToken);
        // A new code always comes with a fresh budget, so a customer who mistyped is never stuck.
        await cache.RemoveAsync(AttemptsKey(telegramUserId), cancellationToken);
    }

    public Task<string?> GetOtpAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<string>(OtpKey(telegramUserId), cancellationToken);

    public async Task ClearOtpAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(OtpKey(telegramUserId), cancellationToken);
        await cache.RemoveAsync(AttemptsKey(telegramUserId), cancellationToken);
    }

    public async Task CountFailedOtpAttemptAsync(long telegramUserId, CancellationToken cancellationToken = default)
    {
        var key = AttemptsKey(telegramUserId);
        var attempts = int.TryParse(await cache.GetAsync<string>(key, cancellationToken), out var parsed) ? parsed : 0;
        attempts++;

        if (attempts >= OtpPolicy.MaxAttempts)
        {
            await ClearOtpAsync(telegramUserId, cancellationToken);
            return;
        }

        await cache.SetAsync(key, attempts.ToString(), OtpTtl, cancellationToken);
    }

    public Task<string?> GetLanguageAsync(long telegramUserId, CancellationToken cancellationToken = default) =>
        cache.GetAsync<string>(LanguageKey(telegramUserId), cancellationToken);

    public Task SetLanguageAsync(long telegramUserId, string culture, CancellationToken cancellationToken = default) =>
        cache.SetAsync(LanguageKey(telegramUserId), culture, LanguageTtl, cancellationToken);

    private static string StateKey(long telegramUserId) => $"bot:state:{telegramUserId}";

    private static string LanguageKey(long telegramUserId) => $"bot:lang:{telegramUserId}";

    private static string OtpKey(long telegramUserId) => $"bot:otp:{telegramUserId}";

    private static string AttemptsKey(long telegramUserId) => $"bot:otp:{telegramUserId}:attempts";
}
