using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Audit;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Orders;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Common;
using PcMarket.Domain.Common;
using DomainEnums = PcMarket.Domain.Enums;
using Dto = PcMarket.Contracts.Orders;

namespace PcMarket.Application.Admin;

/// <summary>Back-office order management: listing/filtering, detail with customer, and status transitions.
/// Transitions go through the order state machine, so they emit the same notifications customers receive.</summary>
public sealed class AdminOrderService(IApplicationDbContext db, IUserDirectory users, IAuditLogger audit)
{
    public async Task<PagedResult<AdminOrderListItemDto>> ListAsync(
        Dto.OrderStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Orders.Include(o => o.Items).AsQueryable();
        if (status is not null)
        {
            var domainStatus = (DomainEnums.OrderStatus)(int)status.Value;
            query = query.Where(o => o.Status == domainStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.Number.Contains(term));
        }

        var total = await query.LongCountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var customers = await users.GetManyAsync(orders.Select(o => o.UserId).Distinct(), ct);

        var items = orders.Select(o => new AdminOrderListItemDto(
            o.Id, o.Number, (Dto.OrderStatus)(int)o.Status, (Dto.PaymentStatus)(int)o.PaymentStatus,
            (Dto.PaymentMethod)(int)o.PaymentMethod, o.Total, o.Items.Sum(i => i.Qty), o.CreatedAt,
            customers.TryGetValue(o.UserId, out var c) ? c.Phone : null)).ToList();

        return new PagedResult<AdminOrderListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdminOrderDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order is null)
        {
            return null;
        }

        var customer = await users.FindAsync(order.UserId, ct);
        var orderCount = await db.Orders.CountAsync(o => o.UserId == order.UserId, ct);
        return new AdminOrderDetailDto(order.ToDto(), ToCustomer(customer, orderCount));
    }

    public async Task<AdminOrderDetailDto> AdvanceStatusAsync(Guid id, Dto.OrderStatus toStatus, string changedBy, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct) ?? throw new DomainException("Order not found.");
        order.TransitionTo((DomainEnums.OrderStatus)(int)toStatus, changedBy);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("order.status-advance", "Order", order.Id.ToString(), $"{order.Number} → {toStatus}", ct);

        var customer = await users.FindAsync(order.UserId, ct);
        var orderCount = await db.Orders.CountAsync(o => o.UserId == order.UserId, ct);
        return new AdminOrderDetailDto(order.ToDto(), ToCustomer(customer, orderCount));
    }

    public Task<AdminOrderDetailDto> RefundAsync(Guid id, string changedBy, CancellationToken ct = default) =>
        AdvanceStatusAsync(id, Dto.OrderStatus.Refunded, changedBy, ct);

    public async Task<AdminCustomerDto?> LookupCustomerAsync(string phone, CancellationToken ct = default)
    {
        var user = await users.FindByPhoneAsync(phone.Trim(), ct);
        if (user is null)
        {
            return null;
        }

        var orderCount = await db.Orders.CountAsync(o => o.UserId == user.Id, ct);
        return ToCustomer(user, orderCount);
    }

    private Task<Domain.Ordering.Order?> LoadAsync(Guid id, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).Include(o => o.StatusHistory).FirstOrDefaultAsync(o => o.Id == id, ct);

    private static AdminCustomerDto? ToCustomer(UserSummary? user, int orderCount) =>
        user is null ? null : new AdminCustomerDto(user.Id, user.Phone, user.FullName, user.Email, orderCount);
}
