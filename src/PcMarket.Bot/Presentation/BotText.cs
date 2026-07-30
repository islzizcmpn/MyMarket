using System.Globalization;
using System.Net;
using System.Text;
using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Orders;

namespace PcMarket.Bot.Presentation;

/// <summary>Renders bot messages. Everything is sent with Telegram's HTML parse mode, so every value that
/// came from the database or a user goes through <see cref="Escape"/> first.</summary>
public static class BotText
{
    private const int MaxDescriptionLength = 300;

    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Money(decimal amount) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} UZS";

    public static string Welcome(string? firstName) =>
        $"""
        <b>Welcome to PcMarket{(string.IsNullOrWhiteSpace(firstName) ? string.Empty : ", " + Escape(firstName))}!</b>

        Browse the catalog, add items to your cart, and check out — all from here.
        Send any text to search for a product.
        """;

    public static string Help() =>
        """
        <b>PcMarket bot</b>

        /catalog — browse categories
        /search — search products
        /cart — view your cart
        /orders — your orders and their status
        /link — link your PcMarket account
        /unlink — unlink this Telegram account
        /help — this message

        Tip: just send any text to search.
        """;

    public static string MainMenu(string? phone) =>
        phone is null
            ? "<b>Main menu</b>\n\nYou are browsing as a guest. Link your account to check out."
            : $"<b>Main menu</b>\n\nSigned in as {Escape(phone)}.";

    public static string Product(ProductDetailDto product)
    {
        var text = new StringBuilder();
        text.Append("<b>").Append(Escape(product.Name)).Append("</b>\n");

        if (!string.IsNullOrWhiteSpace(product.BrandName))
        {
            text.Append("Brand: ").Append(Escape(product.BrandName)).Append('\n');
        }

        var inStock = product.Variants.Where(v => v.StockQty > 0).ToList();
        var prices = product.Variants.Select(v => v.Price).DefaultIfEmpty(0m).ToList();
        text.Append("Price: ").Append(Money(prices.Min()));
        if (prices.Max() != prices.Min())
        {
            text.Append(" – ").Append(Money(prices.Max()));
        }

        text.Append('\n').Append(inStock.Count > 0 ? "In stock" : "Out of stock").Append("\n");

        if (!string.IsNullOrWhiteSpace(product.Description))
        {
            text.Append('\n').Append(Escape(Truncate(product.Description, MaxDescriptionLength))).Append('\n');
        }

        if (product.Specs.Count > 0)
        {
            text.Append("\n<b>Specs</b>\n");
            foreach (var (key, value) in product.Specs.Take(10))
            {
                text.Append("• ").Append(Escape(key)).Append(": ").Append(Escape(value)).Append('\n');
            }
        }

        return text.ToString();
    }

    public static string Cart(CartDto cart)
    {
        if (cart.Items.Count == 0)
        {
            return "<b>Your cart</b>\n\nYour cart is empty. Browse the catalog to add something.";
        }

        var text = new StringBuilder("<b>Your cart</b>\n\n");
        foreach (var item in cart.Items)
        {
            text.Append("• ").Append(Escape(item.ProductName))
                .Append(" ×").Append(item.Qty)
                .Append(" — ").Append(Money(item.LineTotal))
                .Append('\n');
        }

        text.Append("\n<b>Subtotal: ").Append(Money(cart.Subtotal)).Append("</b>");
        return text.ToString();
    }

    public static string Order(OrderDto order)
    {
        var text = new StringBuilder();
        text.Append("<b>Order ").Append(Escape(order.Number)).Append("</b>\n")
            .Append("Status: <b>").Append(order.Status).Append("</b>\n")
            .Append("Payment: ").Append(order.PaymentMethod).Append(" (").Append(order.PaymentStatus).Append(")\n")
            .Append("Placed: ").Append(order.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC\n\n");

        foreach (var item in order.Items)
        {
            text.Append("• ").Append(Escape(item.Name))
                .Append(" ×").Append(item.Qty)
                .Append(" — ").Append(Money(item.LineTotal))
                .Append('\n');
        }

        text.Append("\n<b>Total: ").Append(Money(order.Total)).Append("</b>\n")
            .Append("\nDelivery: ").Append(Escape(Address(order.ShippingAddress)));

        return text.ToString();
    }

    public static string OrderList(IReadOnlyList<OrderListItemDto> orders) =>
        orders.Count == 0
            ? "<b>Your orders</b>\n\nYou have not placed any orders yet."
            : "<b>Your orders</b>\n\nPick an order to see its details and status.";

    public static string OrderButtonLabel(OrderListItemDto order) =>
        $"{order.Number} · {order.Status} · {Money(order.Total)}";

    public static string NewOrderAlert(OrderDto order, string? customerPhone)
    {
        var text = new StringBuilder("<b>🛒 New order ").Append(Escape(order.Number)).Append("</b>\n");
        text.Append("Total: <b>").Append(Money(order.Total)).Append("</b>\n")
            .Append("Payment: ").Append(order.PaymentMethod).Append('\n')
            .Append("Status: ").Append(order.Status).Append('\n');

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            text.Append("Customer: ").Append(Escape(customerPhone)).Append('\n');
        }

        text.Append("Items: ").Append(order.Items.Sum(i => i.Qty)).Append('\n')
            .Append("Deliver to: ").Append(Escape(Address(order.ShippingAddress)));

        return text.ToString();
    }

    public static string Address(ShippingAddressDto address) =>
        string.Join(", ", new[] { address.Region, address.City, address.Street, address.Details }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
