using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;

namespace PcMarket.Application.Abstractions.Payments;

/// <summary>Outcome of initiating a payment for an order.</summary>
/// <param name="Provider">The rail that handled initiation.</param>
/// <param name="RequiresRedirect">True when the customer must be sent to <paramref name="PaymentUrl"/>.</param>
/// <param name="PaymentUrl">Gateway checkout URL for online rails; null for cash.</param>
/// <param name="OrderStatus">The order status after initiation.</param>
public sealed record PaymentInitiationResult(
    PaymentProvider Provider,
    bool RequiresRedirect,
    string? PaymentUrl,
    OrderStatus OrderStatus);

/// <summary>A single payment rail. Implementations live in <c>PcMarket.Payments</c>; the rest of the
/// system stays payment-agnostic and talks only to this contract.</summary>
public interface IPaymentProvider
{
    /// <summary>The checkout method this provider handles.</summary>
    PaymentMethod Method { get; }

    /// <summary>Whether the provider is switched on via configuration (feature flag).</summary>
    bool IsEnabled { get; }

    /// <summary>Advances the order into its post-checkout state and, for online rails, returns the URL to
    /// redirect the customer to. Must be idempotent: calling twice for the same order is safe.</summary>
    Task<PaymentInitiationResult> InitiateAsync(Order order, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the <see cref="IPaymentProvider"/> for a chosen method, honouring feature flags.</summary>
public interface IPaymentProviderResolver
{
    /// <summary>Returns the enabled provider for <paramref name="method"/>.</summary>
    /// <exception cref="PcMarket.Domain.Common.DomainException">If no enabled provider handles the method.</exception>
    IPaymentProvider Resolve(PaymentMethod method);
}
