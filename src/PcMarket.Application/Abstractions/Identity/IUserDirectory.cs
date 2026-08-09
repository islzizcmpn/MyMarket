namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Minimal read view of a user for back-office display (the identity store lives in Infrastructure).</summary>
public sealed record UserSummary(Guid Id, string? Phone, string? FullName, string? Email);

/// <summary>The full identity record behind a customer, as the back-office list needs it. Separate from
/// <see cref="UserSummary"/> because that one is deliberately minimal and is embedded in order payloads;
/// this one is read only by the customer pages and carries what those pages show.</summary>
public sealed record UserAccount(
    Guid Id,
    string? Phone,
    string? FullName,
    string? Email,
    long? TelegramUserId,
    string? Language,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles);

/// <summary>Looks up users for the admin panel without exposing the Identity types to the Application layer.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserSummary?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserSummary>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>One page of accounts, newest first, optionally narrowed by a phone/name/email fragment.
    /// Returns the page and the unpaged total so the caller can build its own paging envelope — the
    /// identity store stays free of the Contracts paging type.</summary>
    Task<(IReadOnlyList<UserAccount> Items, long Total)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
