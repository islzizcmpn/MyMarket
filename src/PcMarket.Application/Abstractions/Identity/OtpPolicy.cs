namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Shared rules for one-time codes, so the API path and the bot path cannot drift apart.
///
/// A six-digit code is only 1,000,000 possibilities, and <see cref="Ttl"/> alone does not make that safe — a
/// caller who may guess without limit will eventually land one, then simply request a fresh code and continue.
/// <see cref="MaxAttempts"/> is what closes that: past the cap the code is discarded outright, so guessing can
/// only resume with a new code, which costs an SMS and is rate limited at the edge.</summary>
public static class OtpPolicy
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    /// <summary>Wrong guesses tolerated before the code is thrown away. Generous enough for a fat-fingered
    /// customer, far too small to brute-force six digits.</summary>
    public const int MaxAttempts = 5;
}
