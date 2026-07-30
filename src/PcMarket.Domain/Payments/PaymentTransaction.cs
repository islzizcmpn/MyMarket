using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;

namespace PcMarket.Domain.Payments;

/// <summary>An entry in the payment ledger: one gateway transaction against an order. Idempotency is
/// enforced on (<see cref="Provider"/>, <see cref="ProviderTxnId"/>) so replayed webhooks apply once.</summary>
public class PaymentTransaction : Entity
{
    public Guid OrderId { get; set; }
    public PaymentProvider Provider { get; set; }

    /// <summary>The gateway's own transaction id; null until the gateway assigns one.</summary>
    public string? ProviderTxnId { get; set; }

    public PaymentTransactionState State { get; set; } = PaymentTransactionState.Created;
    public decimal Amount { get; set; }

    /// <summary>Verbatim gateway request/callback payload, retained for audit, stored as JSONB.</summary>
    public string? RawPayload { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PerformedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public Order Order { get; set; } = null!;
}
