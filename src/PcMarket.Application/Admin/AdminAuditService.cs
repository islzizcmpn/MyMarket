using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Common;

namespace PcMarket.Application.Admin;

/// <summary>Reads the back-office audit trail, most recent first.</summary>
public sealed class AdminAuditService(IApplicationDbContext db)
{
    public async Task<PagedResult<AuditLogEntryDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await db.AuditLog.LongCountAsync(ct);
        var entries = await db.AuditLog
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new AuditLogEntryDto(e.Id, e.CreatedAt, e.ActorName, e.Action, e.EntityType, e.EntityId, e.Summary))
            .ToListAsync(ct);

        return new PagedResult<AuditLogEntryDto>(entries, page, pageSize, total);
    }
}
