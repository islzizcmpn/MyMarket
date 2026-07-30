using System.Globalization;
using PcMarket.Contracts.Orders;

namespace PcMarket.Mobile.Core;

/// <summary>Display formatting shared by the mobile view models. Mirrors the storefront's formatter so the
/// same order shows the same numbers on web and mobile.</summary>
public static class Format
{
    /// <summary>Prices are whole sums in UZS; the currency reads better as a suffix than a symbol.</summary>
    public static string Money(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ') + " so'm";

    public static string Date(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Human-readable order status ("AwaitingPayment" → "Awaiting payment").</summary>
    public static string Status(OrderStatus status) => status switch
    {
        OrderStatus.AwaitingPayment => "Awaiting payment",
        _ => status.ToString()
    };

    public static string PaymentMethod(PaymentMethod method) => method switch
    {
        Contracts.Orders.PaymentMethod.Cash => "Cash on delivery",
        _ => method.ToString()
    };
}
