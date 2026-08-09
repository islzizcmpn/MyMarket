using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.Infrastructure.Identity;

/// <summary>Reads users from the Identity store for back-office display.</summary>
public sealed class UserDirectory(PcMarketDbContext db) : IUserDirectory
{
    public async Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Users.Where(u => u.Id == userId).Select(Project).FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSummary?> FindByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
        await db.Users.Where(u => u.PhoneNumber == phone || u.UserName == phone).Select(Project).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        var users = await db.Users.Where(u => ids.Contains(u.Id)).Select(Project).ToListAsync(cancellationToken);
        return users.ToDictionary(u => u.Id);
    }

    public async Task<(IReadOnlyList<UserAccount> Items, long Total)> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term))
                || (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{term}%"))
                || (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ProjectRow)
            .ToListAsync(cancellationToken);

        return (await WithRolesAsync(rows, cancellationToken), total);
    }

    public async Task<UserAccount?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await db.Users.Where(u => u.Id == userId).Select(ProjectRow).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (await WithRolesAsync([row], cancellationToken))[0];
    }

    /// <summary>Attaches roles in a second query rather than a correlated collection projection, which keeps
    /// the SQL flat and predictable. <c>ApplicationUser</c> deliberately carries no roles navigation, so the
    /// Identity join tables are queried directly.</summary>
    private async Task<IReadOnlyList<UserAccount>> WithRolesAsync(
        IReadOnlyList<AccountRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var pairs = await db.UserRoles
            .Where(ur => ids.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var byUser = pairs
            .Where(p => p.Name is not null)
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Name!).Order().ToList());

        return rows.Select(r => new UserAccount(
            r.Id, r.Phone, r.FullName, r.Email, r.TelegramUserId, r.Language, r.CreatedAt,
            byUser.TryGetValue(r.Id, out var roles) ? roles : [])).ToList();
    }

    /// <summary>The identity columns of one account, before roles are attached.</summary>
    private sealed record AccountRow(
        Guid Id, string? Phone, string? FullName, string? Email,
        long? TelegramUserId, string? Language, DateTimeOffset CreatedAt);

    private static readonly System.Linq.Expressions.Expression<Func<ApplicationUser, UserSummary>> Project =
        u => new UserSummary(u.Id, u.PhoneNumber, u.FullName, u.Email);

    private static readonly System.Linq.Expressions.Expression<Func<ApplicationUser, AccountRow>> ProjectRow =
        u => new AccountRow(u.Id, u.PhoneNumber, u.FullName, u.Email, u.TelegramUserId, u.Language, u.CreatedAt);
}
