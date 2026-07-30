using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Application.Orders;
using PcMarket.Contracts.Payments;
using PcMarket.Domain.Common;

namespace PcMarket.Application.Payments;

/// <summary>Starts (or re-starts) a payment for an existing order by delegating to the order's rail. Safe
/// to call more than once: the provider's initiation is idempotent.</summary>
public sealed class PaymentService(IApplicationDbContext db, IPaymentProviderResolver providers)
{
    public async Task<PaymentInitiationResponse> InitiateAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                    ?? throw new DomainException("Order not found.");

        if (order.UserId != userId)
        {
            throw new DomainException("Order not found.");
        }

        var provider = providers.Resolve(order.PaymentMethod);
        var result = await provider.InitiateAsync(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new PaymentInitiationResponse(
            (PaymentProvider)(int)result.Provider,
            result.RequiresRedirect,
            result.PaymentUrl,
            result.OrderStatus.ToDto());
    }
}
