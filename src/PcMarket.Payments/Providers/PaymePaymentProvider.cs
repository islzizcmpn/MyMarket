using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Payments.Configuration;
using PcMarket.Payments.Payme;
using DomainProvider = PcMarket.Domain.Enums.PaymentProvider;

namespace PcMarket.Payments.Providers;

/// <summary>Payme (Paycom) Merchant API. Initiation builds the hosted-checkout link; settlement happens
/// over JSON-RPC (see <see cref="PaymeRpcService"/>).</summary>
public sealed class PaymePaymentProvider(IApplicationDbContext db, IOptions<PaymentsSettings> settings) : IPaymentProvider
{
    public PaymentMethod Method => PaymentMethod.Payme;

    public bool IsEnabled => settings.Value.Payme.Enabled;

    public async Task<PaymentInitiationResult> InitiateAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Status == OrderStatus.Created)
        {
            order.PaymentStatus = PaymentStatus.Pending;
            order.TransitionTo(OrderStatus.AwaitingPayment, "system:Payme");
        }

        var hasOpenLedger = await db.PaymentTransactions.AnyAsync(
            t => t.OrderId == order.Id
                 && (t.State == PaymentTransactionState.Created || t.State == PaymentTransactionState.Pending),
            cancellationToken);

        if (!hasOpenLedger)
        {
            db.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Provider = DomainProvider.Payme,
                State = PaymentTransactionState.Created,
                Amount = order.Total
            });
        }

        return new PaymentInitiationResult(
            DomainProvider.Payme,
            RequiresRedirect: true,
            PaymentUrl: BuildCheckoutUrl(order),
            order.Status);
    }

    private string BuildCheckoutUrl(Order order)
    {
        var config = settings.Value.Payme;
        var amountTiyin = PaymeAmount.ToTiyin(order.Total);
        var raw = $"m={config.MerchantId};ac.{PaymeAccount.OrderIdField}={order.Number};a={amountTiyin}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return $"{config.CheckoutUrl}/{encoded}";
    }
}
