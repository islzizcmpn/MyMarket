using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;

namespace PcMarket.Application.Orders;

/// <summary>Scheduled maintenance over the order/payment ledger, driven by Hangfire recurring jobs.</summary>
public sealed class OrderMaintenanceService(IApplicationDbContext db, ILogger<OrderMaintenanceService> logger)
{
    /// <summary>Cancels unpaid online orders left in <see cref="OrderStatus.AwaitingPayment"/> past the
    /// timeout, restoring their reserved stock. Orders with a settled payment are never touched.</summary>
    public async Task<int> CancelExpiredOrdersAsync(int timeoutMinutes, CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes);

        var expired = await db.Orders
            .Where(o => o.Status == OrderStatus.AwaitingPayment && o.CreatedAt < threshold)
            .Where(o => !db.PaymentTransactions.Any(t =>
                t.OrderId == o.Id && t.State == PaymentTransactionState.Performed))
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        var variantIds = expired.SelectMany(o => o.Items).Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var order in expired)
        {
            foreach (var item in order.Items)
            {
                if (variants.TryGetValue(item.ProductVariantId, out var variant))
                {
                    variant.StockQty += item.Qty;
                }
            }

            order.TransitionTo(OrderStatus.Cancelled, "job:timeout");
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Auto-cancelled {Count} unpaid order(s) past the {Timeout}m timeout.", expired.Count, timeoutMinutes);
        return expired.Count;
    }

    /// <summary>Repairs orders whose payment was recorded as performed in the ledger but whose status did
    /// not advance (e.g. a crash between the two writes), advancing them to <see cref="OrderStatus.Paid"/>.</summary>
    public async Task<int> ReconcilePendingPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var stuck = await db.Orders
            .Where(o => o.Status == OrderStatus.AwaitingPayment)
            .Where(o => db.PaymentTransactions.Any(t =>
                t.OrderId == o.Id && t.State == PaymentTransactionState.Performed))
            .Include(o => o.StatusHistory)
            .ToListAsync(cancellationToken);

        foreach (var order in stuck)
        {
            order.TransitionTo(OrderStatus.Paid, "job:reconciliation");
        }

        if (stuck.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Reconciliation advanced {Count} order(s) to Paid from a performed ledger entry.", stuck.Count);
        }

        return stuck.Count;
    }
}
