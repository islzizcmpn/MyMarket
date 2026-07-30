namespace PcMarket.Application.Abstractions.Audit;

/// <summary>Records a back-office action against the audit trail, attributing it to the current caller.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string? entityId, string? summary, CancellationToken cancellationToken = default);
}
