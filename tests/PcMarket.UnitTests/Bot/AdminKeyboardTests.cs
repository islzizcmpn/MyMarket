using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Orders;

namespace PcMarket.UnitTests.Bot;

/// <summary>The admin buttons must offer exactly the transitions the order state machine allows, so a
/// manager can never press a button that the domain will then reject.</summary>
public class AdminKeyboardTests
{
    [Fact]
    public void AdminOrderActions_OffersOnlyTheStateMachinesNextStates()
    {
        var orderId = Guid.NewGuid();
        var buttons = Labels(BotKeyboards.AdminOrderActions("en", orderId, OrderStatus.Processing));

        Assert.Contains("➡️ Shipped", buttons);
        Assert.Contains("➡️ Cancelled", buttons);
        Assert.Contains("➡️ Refunded", buttons);
        Assert.DoesNotContain("➡️ Paid", buttons);
        Assert.DoesNotContain("➡️ Delivered", buttons);
    }

    [Fact]
    public void AdminOrderActions_OnATerminalOrder_OffersNoTransitions()
    {
        var buttons = Labels(BotKeyboards.AdminOrderActions("en", Guid.NewGuid(), OrderStatus.Cancelled));

        Assert.DoesNotContain(buttons, label => label.StartsWith("➡️", StringComparison.Ordinal));
    }

    [Fact]
    public void OrderDetail_OffersCancelOnlyWhileCancellingIsLegal()
    {
        Assert.Contains("✖️ Cancel order", Labels(BotKeyboards.OrderDetail("en", Order(OrderStatus.Processing), null)));
        Assert.DoesNotContain("✖️ Cancel order", Labels(BotKeyboards.OrderDetail("en", Order(OrderStatus.Delivered), null)));
    }

    private static List<string> Labels(Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup markup) =>
        markup.InlineKeyboard.SelectMany(row => row).Select(button => button.Text).ToList();

    private static OrderDto Order(OrderStatus status) =>
        new(
            Guid.NewGuid(),
            "PM-1",
            status,
            PaymentMethod.Cash,
            PaymentStatus.None,
            DeliveryType.Courier,
            new ShippingAddressDto("Toshkent shahri", "Chilonzor", "Amir Temur 1", null),
            Subtotal: 100m,
            DeliveryFee: 0m,
            Total: 100m,
            Currency: "UZS",
            CreatedAt: DateTimeOffset.UtcNow,
            Items: [],
            History: []);
}
