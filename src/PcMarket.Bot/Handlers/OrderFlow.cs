using PcMarket.Application.Carts;
using PcMarket.Application.Orders;
using PcMarket.Application.Payments;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Orders;
using PcMarket.Domain.Common;

namespace PcMarket.Bot.Handlers;

/// <summary>Checkout and order tracking. Checkout collects a delivery address across three steps (region
/// button → city → street) held in Redis conversation state, then creates the order through the same
/// <see cref="OrderService"/> the web checkout uses and initiates payment on the chosen rail.</summary>
public sealed class OrderFlow(
    OrderService orders,
    PaymentService payments,
    CartService carts,
    BotSession session,
    IConversationStore conversations,
    BotResponder responder)
{
    public async Task BeginCheckoutAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.CheckoutNeedsAccount),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var cart = await carts.GetCartAsync(link.UserId, null, cancellationToken);
        if (cart.Items.Count == 0)
        {
            await responder.ReplyAsync(
                context,
                BotText.Cart(context.Culture, cart),
                BotKeyboards.Cart(context.Culture, cart),
                cancellationToken);
            return;
        }

        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { Stage = BotStage.None, Region = null, City = null, Street = null },
            cancellationToken);

        await responder.ReplyAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.CheckoutHeader, BotText.Money(context.Culture, cart.Subtotal)),
            BotKeyboards.Regions(context.Culture),
            cancellationToken);
    }

    public async Task SetRegionAsync(BotContext context, int regionIndex, CancellationToken cancellationToken = default)
    {
        if (regionIndex < 0 || regionIndex >= UzbekistanRegions.All.Count)
        {
            await BeginCheckoutAsync(context, cancellationToken);
            return;
        }

        var region = UzbekistanRegions.All[regionIndex];
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { Region = region, Stage = BotStage.AwaitingCity },
            cancellationToken);

        await responder.ReplyAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.RegionChosen, BotText.Escape(region)),
            keyboard: null,
            cancellationToken);
    }

    public async Task SetCityAsync(BotContext context, string city, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { City = city.Trim(), Stage = BotStage.AwaitingStreet },
            cancellationToken);

        await responder.ReplyAsync(
            context,
            BotPhrases.Get(context.Culture, Phrase.CityChosen),
            keyboard: null,
            cancellationToken);
    }

    public async Task SetStreetAsync(BotContext context, string street, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        var updated = state with { Street = street.Trim(), Stage = BotStage.None };
        await conversations.SetAsync(context.TelegramUserId, updated, cancellationToken);

        var address = $"{updated.Region}, {updated.City}, {updated.Street}";
        await responder.ReplyAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.DeliveringTo, BotText.Escape(address)),
            BotKeyboards.PaymentMethods(context.Culture),
            cancellationToken);
    }

    public async Task PlaceOrderAsync(BotContext context, PaymentMethod method, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.LinkToPlaceOrder),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        if (state.Region is null || state.City is null || state.Street is null)
        {
            await BeginCheckoutAsync(context, cancellationToken);
            return;
        }

        var request = new CreateOrderRequest(
            method,
            DeliveryType.Courier,
            AddressId: null,
            new ShippingAddressDto(state.Region, state.City, state.Street, null));

        var order = await orders.CreateAsync(link.UserId, request, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { Stage = BotStage.None, Region = null, City = null, Street = null },
            cancellationToken);

        string? paymentUrl = null;
        if (method != PaymentMethod.Cash)
        {
            var initiation = await payments.InitiateAsync(link.UserId, order.Id, cancellationToken);
            paymentUrl = initiation.PaymentUrl;
        }

        // Re-read so the message shows the status the payment rail left the order in.
        var placed = await orders.GetAsync(link.UserId, order.Id, cancellationToken) ?? order;
        var header = BotPhrases.Get(
            context.Culture,
            paymentUrl is null ? Phrase.OrderPlacedHeader : Phrase.OrderPlacedPayHeader);

        await responder.ReplyAsync(
            context,
            header + BotText.Order(context.Culture, placed),
            BotKeyboards.OrderDetail(context.Culture, placed, paymentUrl),
            cancellationToken);
    }

    public async Task ShowOrdersAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.LinkToSeeOrders),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var list = await orders.ListAsync(link.UserId, cancellationToken);
        await responder.ReplyAsync(
            context,
            BotText.OrderList(context.Culture, list),
            BotKeyboards.Orders(context.Culture, list),
            cancellationToken);
    }

    public async Task ShowOrderAsync(BotContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.LinkToViewOrder),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var order = await orders.GetAsync(link.UserId, orderId, cancellationToken);
        if (order is null)
        {
            await ShowOrdersAsync(context, cancellationToken);
            return;
        }

        await responder.ReplyAsync(
            context,
            BotText.Order(context.Culture, order),
            BotKeyboards.OrderDetail(context.Culture, order, null),
            cancellationToken);
    }

    public async Task CancelOrderAsync(BotContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.LinkToManageOrders),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var order = await orders.CancelAsync(link.UserId, orderId, cancellationToken);
        await responder.AcknowledgeAsync(context, BotPhrases.Get(context.Culture, Phrase.OrderCancelledToast), cancellationToken);
        await responder.ReplyAsync(
            context,
            BotText.Order(context.Culture, order),
            BotKeyboards.OrderDetail(context.Culture, order, null),
            cancellationToken);
    }

    public async Task PayOrderAsync(BotContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.LinkToPayOrders),
                BotKeyboards.MainMenu(context.Culture, false),
                cancellationToken);
            return;
        }

        var initiation = await payments.InitiateAsync(link.UserId, orderId, cancellationToken);
        var order = await orders.GetAsync(link.UserId, orderId, cancellationToken);
        if (order is null)
        {
            await ShowOrdersAsync(context, cancellationToken);
            return;
        }

        var text = initiation.PaymentUrl is null
            ? BotText.Order(context.Culture, order)
            : BotPhrases.Get(context.Culture, Phrase.TapPayNow) + BotText.Order(context.Culture, order);

        await responder.ReplyAsync(
            context,
            text,
            BotKeyboards.OrderDetail(context.Culture, order, initiation.PaymentUrl),
            cancellationToken);
    }
}
