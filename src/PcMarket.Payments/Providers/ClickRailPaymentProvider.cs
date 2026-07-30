using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Payments.Configuration;
using DomainProvider = PcMarket.Domain.Enums.PaymentProvider;

namespace PcMarket.Payments.Providers;

/// <summary>Shared initiation logic for every rail that settles through Click's Merchant API. Uzcard and
/// Humo ride the same rail as Click itself, so they derive from this base and differ only by their
/// method/provider tag and feature flag.</summary>
public abstract class ClickRailPaymentProvider(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
    : IPaymentProvider
{
    protected PaymentsSettings Root => settings.Value;

    public abstract PaymentMethod Method { get; }

    /// <summary>The ledger tag recorded on the <see cref="PaymentTransaction"/> for this rail.</summary>
    protected abstract DomainProvider LedgerProvider { get; }

    /// <summary>The Click credentials this rail authenticates with.</summary>
    protected abstract ClickSettings ClickConfig { get; }

    public bool IsEnabled => ClickConfig.Enabled;

    public async Task<PaymentInitiationResult> InitiateAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Status == OrderStatus.Created)
        {
            order.PaymentStatus = PaymentStatus.Pending;
            order.TransitionTo(OrderStatus.AwaitingPayment, $"system:{Method}");
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
                Provider = LedgerProvider,
                State = PaymentTransactionState.Created,
                Amount = order.Total
            });
        }

        return new PaymentInitiationResult(
            (PaymentProvider)(int)LedgerProvider,
            RequiresRedirect: true,
            PaymentUrl: BuildCheckoutUrl(order),
            order.Status);
    }

    private string BuildCheckoutUrl(Order order)
    {
        var amount = order.Total.ToString("0.##", CultureInfo.InvariantCulture);
        var url = $"{ClickConfig.CheckoutUrl}?service_id={Uri.EscapeDataString(ClickConfig.ServiceId)}" +
                  $"&merchant_id={Uri.EscapeDataString(ClickConfig.MerchantId)}" +
                  $"&amount={amount}" +
                  $"&transaction_param={Uri.EscapeDataString(order.Number)}";

        if (!string.IsNullOrWhiteSpace(ClickConfig.ReturnUrl))
        {
            url += $"&return_url={Uri.EscapeDataString(ClickConfig.ReturnUrl)}";
        }

        return url;
    }
}

/// <summary>Click card/wallet payments.</summary>
public sealed class ClickPaymentProvider(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
    : ClickRailPaymentProvider(db, settings)
{
    public override PaymentMethod Method => PaymentMethod.Click;
    protected override DomainProvider LedgerProvider => DomainProvider.Click;
    protected override ClickSettings ClickConfig => Root.Click;
}

/// <summary>Uzcard, settled over the Click rail.</summary>
public sealed class UzcardPaymentProvider(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
    : ClickRailPaymentProvider(db, settings)
{
    public override PaymentMethod Method => PaymentMethod.Uzcard;
    protected override DomainProvider LedgerProvider => DomainProvider.Uzcard;
    protected override ClickSettings ClickConfig => Root.Uzcard;
}

/// <summary>Humo, settled over the Click rail.</summary>
public sealed class HumoPaymentProvider(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
    : ClickRailPaymentProvider(db, settings)
{
    public override PaymentMethod Method => PaymentMethod.Humo;
    protected override DomainProvider LedgerProvider => DomainProvider.Humo;
    protected override ClickSettings ClickConfig => Root.Humo;
}
