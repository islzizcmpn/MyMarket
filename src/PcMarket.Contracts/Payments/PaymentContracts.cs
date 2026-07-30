using PcMarket.Contracts.Orders;

namespace PcMarket.Contracts.Payments;

/// <summary>The concrete rail a payment runs on. Mirrors the domain enum by value.</summary>
public enum PaymentProvider
{
    Cash = 0,
    Click = 1,
    Payme = 2,
    Uzcard = 3,
    Humo = 4
}

public sealed record PaymentInitiateRequest(Guid OrderId);

/// <summary>Result of starting a payment. Online rails return a <see cref="PaymentUrl"/> to redirect the
/// customer to; cash returns <see cref="RequiresRedirect"/> = false with the order already advanced.</summary>
public sealed record PaymentInitiationResponse(
    PaymentProvider Provider,
    bool RequiresRedirect,
    string? PaymentUrl,
    OrderStatus OrderStatus);
