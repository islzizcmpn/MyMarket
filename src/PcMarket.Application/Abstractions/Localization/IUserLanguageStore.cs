namespace PcMarket.Application.Abstractions.Localization;

/// <summary>Reads and writes the language a customer chose for themselves. This is the durable half of the
/// preference: clients that have no account yet (a Telegram guest, say) keep theirs in their own session store
/// and hand it over here once they link.</summary>
public interface IUserLanguageStore
{
    /// <summary>The stored code, or null when the user never picked one.</summary>
    Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The stored code for the account linked to a Telegram user, or null when there is no such
    /// account or it has no preference.</summary>
    Task<string?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken = default);

    /// <summary>Stores a supported code; unsupported input is normalized by the caller.</summary>
    Task SetAsync(Guid userId, string culture, CancellationToken cancellationToken = default);
}
