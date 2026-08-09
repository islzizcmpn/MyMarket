using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Common;
using DomainEnums = PcMarket.Domain.Enums;
using Dto = PcMarket.Contracts.Orders;

namespace PcMarket.Application.Admin;

/// <summary>Back-office customer directory: who has an account, what they have bought, and where they asked
/// for it. Accounts live in the Identity store and orders in the application store, so the two are read
/// separately and joined here — there is no navigation between them by design.</summary>
public sealed class AdminCustomerService(IApplicationDbContext db, IUserDirectory users)
{
    /// <summary>Orders that money was never kept for. Excluded from lifetime spend, which would otherwise
    /// credit a customer for baskets that were cancelled or handed back.</summary>
    private static readonly DomainEnums.OrderStatus[] UnpaidStatuses =
        [DomainEnums.OrderStatus.Cancelled, DomainEnums.OrderStatus.Refunded];

    public async Task<PagedResult<AdminCustomerListItemDto>> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var (accounts, total) = await users.SearchAsync(search, page, pageSize, ct);
        var items = await WithOrderTotalsAsync(accounts, ct);
        return new PagedResult<AdminCustomerListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdminCustomerDetailDto?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await users.GetAccountAsync(userId, ct);
        if (account is null)
        {
            return null;
        }

        var customer = (await WithOrderTotalsAsync([account], ct))[0];

        var addresses = await db.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .Select(a => new AdminCustomerAddressDto(a.Region, a.City, a.Street, a.Details, a.IsDefault))
            .ToListAsync(ct);

        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        var orderItems = orders.Select(o => new AdminOrderListItemDto(
            o.Id, o.Number, (Dto.OrderStatus)(int)o.Status, (Dto.PaymentStatus)(int)o.PaymentStatus,
            (Dto.PaymentMethod)(int)o.PaymentMethod, o.Total, o.Items.Sum(i => i.Qty), o.CreatedAt,
            account.Phone)).ToList();

        // Every pin the customer has shared, newest first. Orders keep their own copy of the address, so
        // this is a history of where they actually asked for delivery rather than what is on file now.
        var locations = orders
            .Where(o => o.ShippingAddress.Latitude is not null && o.ShippingAddress.Longitude is not null)
            .Select(o => new AdminCustomerLocationDto(
                o.Id,
                o.Number,
                Written(o.ShippingAddress.Region, o.ShippingAddress.City, o.ShippingAddress.Street, o.ShippingAddress.Details),
                o.ShippingAddress.Latitude!.Value,
                o.ShippingAddress.Longitude!.Value,
                o.CreatedAt))
            .ToList();

        return new AdminCustomerDetailDto(customer, addresses, locations, orderItems);
    }

    /// <summary>Counts each account's orders and sums what they actually paid, in one grouped query over the
    /// page rather than one query per customer.</summary>
    private async Task<IReadOnlyList<AdminCustomerListItemDto>> WithOrderTotalsAsync(
        IReadOnlyList<UserAccount> accounts, CancellationToken ct)
    {
        var ids = accounts.Select(a => a.Id).ToList();
        var totals = await db.Orders
            .Where(o => ids.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Spent = g.Where(o => !UnpaidStatuses.Contains(o.Status)).Sum(o => (decimal?)o.Total) ?? 0m
            })
            .ToDictionaryAsync(x => x.UserId, ct);

        return accounts.Select(a =>
        {
            totals.TryGetValue(a.Id, out var t);
            return new AdminCustomerListItemDto(
                a.Id, a.Phone, a.FullName, a.Email, a.Roles,
                a.TelegramUserId is not null, a.Language,
                t?.Count ?? 0, t?.Spent ?? 0m, a.CreatedAt);
        }).ToList();
    }

    private static string Written(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
