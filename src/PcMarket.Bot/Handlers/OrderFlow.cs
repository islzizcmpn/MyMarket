using PcMarket.Application.Carts;
using PcMarket.Application.Orders;
using PcMarket.Application.Payments;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Orders;
using PcMarket.Domain.Common;
using Telegram.Bot.Types.ReplyMarkups;

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
            state with { Stage = BotStage.AwaitingLocation, Latitude = null, Longitude = null, House = null },
            cancellationToken);

        // The prompt edits the message the button was on, but the request-location button lives on a *reply*
        // keyboard, which can only arrive with a new message - hence the two sends.
        await responder.ReplyAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.CheckoutHeader, BotText.Money(context.Culture, cart.Subtotal)),
            keyboard: null,
            cancellationToken);

        await responder.SendAsync(
            context.ChatId,
            BotPhrases.Get(context.Culture, Phrase.ShareLocationButton),
            BotKeyboards.RequestLocation(context.Culture),
            cancellationToken);
    }

    /// <summary>Accepts the pin Telegram delivered and moves on to the one thing a pin cannot say: which door.</summary>
    public async Task SetLocationAsync(
        BotContext context,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { Latitude = latitude, Longitude = longitude, Stage = BotStage.AwaitingHouse },
            cancellationToken);

        // Clearing the reply keyboard matters: leaving it up invites a second pin where a flat number is
        // expected, and the customer would have no obvious way back to their normal keyboard.
        await responder.SendAsync(
            context.ChatId,
            BotPhrases.Get(context.Culture, Phrase.LocationReceived),
            new ReplyKeyboardRemove(),
            cancellationToken);
    }

    /// <summary>Nudges a customer who typed an address instead of sharing a pin. Checkout cannot continue
    /// without one, so this repeats the request rather than falling back to a typed address.</summary>
    public Task AskForLocationAsync(BotContext context, CancellationToken cancellationToken = default) =>
        responder.SendAsync(
            context.ChatId,
            BotPhrases.Get(context.Culture, Phrase.LocationNeeded),
            BotKeyboards.RequestLocation(context.Culture),
            cancellationToken);

    public async Task SetHouseAsync(BotContext context, string house, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        if (state.Latitude is null || state.Longitude is null)
        {
            // The pin expired out of Redis mid-checkout; asking again beats placing an order nobody can deliver.
            await AskForLocationAsync(context, cancellationToken);
            return;
        }

        var updated = state with { House = house.Trim(), Stage = BotStage.None };
        await conversations.SetAsync(context.TelegramUserId, updated, cancellationToken);

        await responder.ReplyAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.DeliveringTo, BotText.Escape(updated.House)),
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
        if (state.Latitude is null || state.Longitude is null || state.House is null)
        {
            await BeginCheckoutAsync(context, cancellationToken);
            return;
        }

        // Region and city stay empty for a bot order: the pin says where this is, far more precisely than a
        // region name would, and inventing one from coordinates would need a geocoding service.
        var request = new CreateOrderRequest(
            method,
            DeliveryType.Courier,
            AddressId: null,
            new ShippingAddressDto(
                Region: string.Empty,
                City: string.Empty,
                Street: state.House,
                Details: null,
                Latitude: state.Latitude,
                Longitude: state.Longitude));

        var order = await orders.CreateAsync(link.UserId, request, cancellationToken);
        await conversations.SetAsync(
            context.TelegramUserId,
            state with { Stage = BotStage.None, Latitude = null, Longitude = null, House = null },
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
