using Microsoft.AspNetCore.Identity;
using PcMarket.Application.Abstractions.Audit;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Admin;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Audit;

/// <summary>Writes audit entries attributed to the current caller (resolved from the JWT).</summary>
public sealed class AuditLogger(IApplicationDbContext db, ICurrentUser currentUser, UserManager<ApplicationUser> userManager) : IAuditLogger
{
    public async Task LogAsync(string action, string entityType, string? entityId, string? summary, CancellationToken cancellationToken = default)
    {
        var actorId = currentUser.UserId;
        string? actorName = null;
        if (actorId is not null)
        {
            var user = await userManager.FindByIdAsync(actorId.Value.ToString());
            actorName = user?.PhoneNumber ?? user?.UserName;
        }

        db.AuditLog.Add(new AuditLogEntry
        {
            ActorUserId = actorId,
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
