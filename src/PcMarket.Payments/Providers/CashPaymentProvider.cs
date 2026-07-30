using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Payments.Configuration;

namespace PcMarket.Payments.Providers;

/// <summary>Cash on delivery. No gateway call: the order is confirmed straight into
/// <see cref="OrderStatus.Processing"/> and settled when the courier collects payment.</summary>
public sealed class CashPaymentProvider(IOptions<PaymentsSettings> settings) : IPaymentProvider
{
    public PaymentMethod Method => PaymentMethod.Cash;

    public bool IsEnabled => settings.Value.Cash.Enabled;

    public Task<PaymentInitiationResult> InitiateAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Status == OrderStatus.Created)
        {
            order.TransitionTo(OrderStatus.Processing, "system:cod");
        }

        return Task.FromResult(new PaymentInitiationResult(
            PaymentProvider.Cash,
            RequiresRedirect: false,
            PaymentUrl: null,
            order.Status));
    }
}
