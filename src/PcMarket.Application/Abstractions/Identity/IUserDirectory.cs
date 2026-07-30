namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Minimal read view of a user for back-office display (the identity store lives in Infrastructure).</summary>
public sealed record UserSummary(Guid Id, string? Phone, string? FullName, string? Email);

/// <summary>Looks up users for the admin panel without exposing the Identity types to the Application layer.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserSummary?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserSummary>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
