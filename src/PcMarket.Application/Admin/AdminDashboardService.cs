using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Admin;
using PcMarket.Domain.Enums;

namespace PcMarket.Application.Admin;

/// <summary>Aggregate figures for the admin dashboard.</summary>
public sealed class AdminDashboardService(IApplicationDbContext db)
{
    private const int LowStockThreshold = 5;

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var startOfToday = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var paidStatuses = new[] { OrderStatus.Paid, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };

        var totalOrders = await db.Orders.CountAsync(ct);
        var ordersToday = await db.Orders.CountAsync(o => o.CreatedAt >= startOfToday, ct);
        var pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.Processing, ct);

        var revenueTotal = await db.Orders.Where(o => paidStatuses.Contains(o.Status)).SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
        var revenueToday = await db.Orders
            .Where(o => paidStatuses.Contains(o.Status) && o.CreatedAt >= startOfToday)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var productCount = await db.Products.CountAsync(ct);
        var lowStock = await db.ProductVariants.CountAsync(v => v.IsActive && v.StockQty <= LowStockThreshold, ct);

        return new DashboardStatsDto(totalOrders, ordersToday, pendingOrders, revenueTotal, revenueToday, productCount, lowStock);
    }
}
