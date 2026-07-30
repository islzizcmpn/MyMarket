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

    private static readonly System.Linq.Expressions.Expression<Func<ApplicationUser, UserSummary>> Project =
        u => new UserSummary(u.Id, u.PhoneNumber, u.FullName, u.Email);
}
