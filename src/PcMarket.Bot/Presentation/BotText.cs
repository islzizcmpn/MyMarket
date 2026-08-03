using System.Globalization;
using System.Net;
using System.Text;
using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Common;
using PcMarket.Contracts.Orders;

namespace PcMarket.Bot.Presentation;

/// <summary>Renders bot messages. Everything is sent with Telegram's HTML parse mode, so every value that
/// came from the database or a user goes through <see cref="Escape"/> first. Wording comes from
/// <see cref="BotPhrases"/> in the culture the update is being answered in.</summary>
public static class BotText
{
    private const int MaxDescriptionLength = 300;

    /// <summary>Amounts are written the way the storefront writes them — grouped by spaces — with the currency
    /// word in the reader's language.</summary>
    private static readonly CultureInfo MoneyFormat = CreateMoneyFormat();

    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Money(string culture, decimal amount) =>
        $"{amount.ToString("#,0", MoneyFormat)} {BotPhrases.Get(culture, Phrase.Currency)}";

    public static string Welcome(string culture, string? firstName) =>
        string.IsNullOrWhiteSpace(firstName)
            ? BotPhrases.Get(culture, Phrase.Welcome)
            : BotPhrases.Format(culture, Phrase.WelcomeNamed, Escape(firstName));

    public static string Help(string culture) => BotPhrases.Get(culture, Phrase.Help);

    public static string MainMenu(string culture, string? phone) =>
        phone is null
            ? BotPhrases.Get(culture, Phrase.MainMenuGuest)
            : BotPhrases.Format(culture, Phrase.MainMenuSignedIn, Escape(phone));

    public static string Product(string culture, ProductDetailDto product)
    {
        var text = new StringBuilder();
        text.Append("<b>").Append(Escape(product.Name)).Append("</b>\n");

        if (!string.IsNullOrWhiteSpace(product.BrandName))
        {
            text.Append(BotPhrases.Get(culture, Phrase.ProductBrand)).Append(": ").Append(Escape(product.BrandName)).Append('\n');
        }

        var inStock = product.Variants.Where(v => v.StockQty > 0).ToList();
        var prices = product.Variants.Select(v => v.Price).DefaultIfEmpty(0m).ToList();
        text.Append(BotPhrases.Get(culture, Phrase.ProductPrice)).Append(": ").Append(Money(culture, prices.Min()));
        if (prices.Max() != prices.Min())
        {
            text.Append(" – ").Append(Money(culture, prices.Max()));
        }

        text.Append('\n')
            .Append(BotPhrases.Get(culture, inStock.Count > 0 ? Phrase.ProductInStock : Phrase.ProductOutOfStock))
            .Append("\n");

        if (!string.IsNullOrWhiteSpace(product.Description))
        {
            text.Append('\n').Append(Escape(Truncate(product.Description, MaxDescriptionLength))).Append('\n');
        }

        if (product.Specs.Count > 0)
        {
            text.Append('\n').Append(BotPhrases.Get(culture, Phrase.ProductSpecs)).Append('\n');
            foreach (var (key, value) in product.Specs.Take(10))
            {
                text.Append("• ").Append(Escape(key)).Append(": ").Append(Escape(value)).Append('\n');
            }
        }

        return text.ToString();
    }

    public static string Cart(string culture, CartDto cart)
    {
        if (cart.Items.Count == 0)
        {
            return BotPhrases.Get(culture, Phrase.CartEmpty);
        }

        var text = new StringBuilder(BotPhrases.Get(culture, Phrase.CartTitle)).Append("\n\n");
        foreach (var item in cart.Items)
        {
            text.Append("• ").Append(Escape(item.ProductName))
                .Append(" ×").Append(item.Qty)
                .Append(" — ").Append(Money(culture, item.LineTotal))
                .Append('\n');
        }

        text.Append("\n<b>").Append(BotPhrases.Get(culture, Phrase.CartSubtotal)).Append(": ")
            .Append(Money(culture, cart.Subtotal)).Append("</b>");
        return text.ToString();
    }

    public static string Order(string culture, OrderDto order)
    {
        var text = new StringBuilder();
        text.Append(BotPhrases.Format(culture, Phrase.OrderTitle, Escape(order.Number))).Append('\n')
            .Append(BotPhrases.Get(culture, Phrase.OrderStatusLabel)).Append(": <b>")
            .Append(BotPhrases.OrderStatusName(culture, order.Status)).Append("</b>\n")
            .Append(BotPhrases.Get(culture, Phrase.OrderPaymentLabel)).Append(": ")
            .Append(BotPhrases.PaymentMethodName(culture, order.PaymentMethod)).Append(" (")
            .Append(BotPhrases.PaymentStatusName(culture, order.PaymentStatus)).Append(")\n")
            .Append(BotPhrases.Get(culture, Phrase.OrderPlacedLabel)).Append(": ")
            .Append(order.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC\n\n");

        foreach (var item in order.Items)
        {
            text.Append("• ").Append(Escape(item.Name))
                .Append(" ×").Append(item.Qty)
                .Append(" — ").Append(Money(culture, item.LineTotal))
                .Append('\n');
        }

        text.Append("\n<b>").Append(BotPhrases.Get(culture, Phrase.OrderTotalLabel)).Append(": ")
            .Append(Money(culture, order.Total)).Append("</b>\n")
            .Append('\n').Append(BotPhrases.Get(culture, Phrase.OrderDeliveryLabel)).Append(": ")
            .Append(Escape(Address(order.ShippingAddress)));

        return text.ToString();
    }

    public static string OrderList(string culture, IReadOnlyList<OrderListItemDto> orders) =>
        BotPhrases.Get(culture, orders.Count == 0 ? Phrase.OrdersEmpty : Phrase.OrdersPick);

    public static string OrderButtonLabel(string culture, OrderListItemDto order) =>
        $"{order.Number} · {BotPhrases.OrderStatusName(culture, order.Status)} · {Money(culture, order.Total)}";

    public static string NewOrderAlert(string culture, OrderDto order, string? customerPhone)
    {
        var text = new StringBuilder(BotPhrases.Format(culture, Phrase.AlertNewOrder, Escape(order.Number))).Append('\n');
        text.Append(BotPhrases.Get(culture, Phrase.AlertTotal)).Append(": <b>").Append(Money(culture, order.Total)).Append("</b>\n")
            .Append(BotPhrases.Get(culture, Phrase.AlertPayment)).Append(": ")
            .Append(BotPhrases.PaymentMethodName(culture, order.PaymentMethod)).Append('\n')
            .Append(BotPhrases.Get(culture, Phrase.AlertStatus)).Append(": ")
            .Append(BotPhrases.OrderStatusName(culture, order.Status)).Append('\n');

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            text.Append(BotPhrases.Get(culture, Phrase.AlertCustomer)).Append(": ").Append(Escape(customerPhone)).Append('\n');
        }

        text.Append(BotPhrases.Get(culture, Phrase.AlertItems)).Append(": ").Append(order.Items.Sum(i => i.Qty)).Append('\n')
            .Append(BotPhrases.Get(culture, Phrase.AlertDeliverTo)).Append(": ").Append(Escape(Address(order.ShippingAddress)));

        // For a bot order the pin is the address, so the manager gets it as tappable links rather than as
        // numbers they would have to copy into a map app themselves.
        if (order.ShippingAddress is { Latitude: { } latitude, Longitude: { } longitude })
        {
            text.Append('\n').Append(BotPhrases.Get(culture, Phrase.AlertLocation)).Append(": ")
                .Append("<a href=\"").Append(MapLinks.Google(latitude, longitude)).Append("\">Google</a>")
                .Append(" · ")
                .Append("<a href=\"").Append(MapLinks.Yandex(latitude, longitude)).Append("\">Yandex</a>")
                .Append(" (").Append(MapLinks.Coordinates(latitude, longitude)).Append(')');
        }

        return text.ToString();
    }

    public static string Address(ShippingAddressDto address) =>
        string.Join(", ", new[] { address.Region, address.City, address.Street, address.Details }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    private static CultureInfo CreateMoneyFormat()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalDigits = 0;
        return culture;
    }
}
