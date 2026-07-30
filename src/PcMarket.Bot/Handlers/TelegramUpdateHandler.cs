using Microsoft.Extensions.Logging;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Orders;
using PcMarket.Domain.Common;
using Telegram.Bot.Types;

namespace PcMarket.Bot.Handlers;

/// <summary>Routes one Telegram update to a flow. Commands and button payloads are the only vocabulary;
/// anything else typed is treated as a product search (or as input for whatever step the conversation is
/// on). Business rule violations surface as the <see cref="DomainException"/> message the rest of the
/// system already produces, so the bot never invents its own wording for "out of stock" and friends.</summary>
public sealed class TelegramUpdateHandler(
    AccountFlow account,
    CatalogFlow catalog,
    CartFlow cart,
    OrderFlow orders,
    AdminFlow admin,
    IConversationStore conversations,
    BotResponder responder,
    ILogger<TelegramUpdateHandler> logger)
{
    public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
    {
        var context = ToContext(update);
        if (context is null)
        {
            logger.LogDebug("Ignoring Telegram update {UpdateId} of type {Type}.", update.Id, update.Type);
            return;
        }

        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                await responder.AcknowledgeAsync(context, toast: null, cancellationToken);
                await HandleCallbackAsync(context, CallbackData.Parse(callbackQuery.Data), cancellationToken);
            }
            else if (update.Message is { } message)
            {
                await HandleMessageAsync(context, message, cancellationToken);
            }
        }
        catch (DomainException ex)
        {
            await responder.ReplyAsync(context, $"⚠️ {BotText.Escape(ex.Message)}", BotKeyboards.MainMenu(true), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handling Telegram update {UpdateId} failed.", update.Id);
            await responder.ReplyAsync(
                context,
                "Something went wrong on our side. Please try again.",
                BotKeyboards.MainMenu(true),
                cancellationToken);
        }
    }

    private async Task HandleMessageAsync(BotContext context, Message message, CancellationToken cancellationToken)
    {
        if (message.Contact is { PhoneNumber: { } sharedPhone } contact)
        {
            // Telegram lets a user share ANY card from their address book, not just their own. Only a card whose
            // UserId is the sender's proves *they* own that number — Telegram verified it at signup and hands it
            // over from its own servers. Anyone else's card is worth no more than a typed number, so it falls
            // through to the OTP path rather than being trusted.
            var ownedBySender = contact.UserId == context.TelegramUserId;
            await account.HandlePhoneAsync(context, sharedPhone, ownedBySender, cancellationToken);
            return;
        }

        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.StartsWith('/'))
        {
            await HandleCommandAsync(context, text, cancellationToken);
            return;
        }

        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        switch (state.Stage)
        {
            case BotStage.AwaitingPhone:
                // Typed, so nothing has vouched for it: this always goes through the OTP.
                await account.HandlePhoneAsync(context, text, phoneVerified: false, cancellationToken);
                break;
            case BotStage.AwaitingOtp:
                await account.HandleOtpAsync(context, text, cancellationToken);
                break;
            case BotStage.AwaitingCity:
                await orders.SetCityAsync(context, text, cancellationToken);
                break;
            case BotStage.AwaitingStreet:
                await orders.SetStreetAsync(context, text, cancellationToken);
                break;
            default:
                await catalog.SearchAsync(context, text, cancellationToken);
                break;
        }
    }

    private Task HandleCommandAsync(BotContext context, string text, CancellationToken cancellationToken)
    {
        // Commands may arrive as "/start@BotName" in groups.
        var command = text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
        var argument = text.Split(' ', 2) is [_, var rest] ? rest.Trim() : string.Empty;

        return command switch
        {
            "/start" => account.StartAsync(context, cancellationToken),
            "/menu" => account.ShowMenuAsync(context, cancellationToken),
            "/help" => account.ShowHelpAsync(context, cancellationToken),
            "/catalog" => catalog.ShowCategoriesAsync(context, cancellationToken),
            "/search" => string.IsNullOrEmpty(argument)
                ? catalog.PromptSearchAsync(context, cancellationToken)
                : catalog.SearchAsync(context, argument, cancellationToken),
            "/cart" => cart.ShowCartAsync(context, cancellationToken),
            "/orders" => orders.ShowOrdersAsync(context, cancellationToken),
            "/link" => account.BeginLinkAsync(context, cancellationToken),
            "/unlink" => account.UnlinkAsync(context, cancellationToken),
            _ => account.ShowHelpAsync(context, cancellationToken)
        };
    }

    private Task HandleCallbackAsync(BotContext context, CallbackData data, CancellationToken cancellationToken) =>
        data.Command switch
        {
            BotCommands.Home => account.ShowMenuAsync(context, cancellationToken),
            BotCommands.Link => account.BeginLinkAsync(context, cancellationToken),
            BotCommands.Categories => catalog.ShowCategoriesAsync(context, cancellationToken),
            BotCommands.Category when data.GuidArg(0) is { } categoryId =>
                catalog.ShowCategoryAsync(context, categoryId, data.IntArg(1, 1), cancellationToken),
            BotCommands.Product when data.GuidArg(0) is { } productId =>
                catalog.ShowProductAsync(context, productId, cancellationToken),
            BotCommands.Search => catalog.PromptSearchAsync(context, cancellationToken),
            BotCommands.AddToCart when data.GuidArg(0) is { } variantId =>
                cart.AddAsync(context, variantId, cancellationToken),
            BotCommands.RemoveItem when data.GuidArg(0) is { } itemId =>
                cart.RemoveAsync(context, itemId, cancellationToken),
            BotCommands.Cart => cart.ShowCartAsync(context, cancellationToken),
            BotCommands.Checkout => orders.BeginCheckoutAsync(context, cancellationToken),
            BotCommands.Region => orders.SetRegionAsync(context, data.IntArg(0, -1), cancellationToken),
            BotCommands.PaymentMethod when TryPaymentMethod(data.IntArg(0, -1), out var method) =>
                orders.PlaceOrderAsync(context, method, cancellationToken),
            BotCommands.Orders => orders.ShowOrdersAsync(context, cancellationToken),
            BotCommands.Order when data.GuidArg(0) is { } orderId =>
                orders.ShowOrderAsync(context, orderId, cancellationToken),
            BotCommands.CancelOrder when data.GuidArg(0) is { } orderId =>
                orders.CancelOrderAsync(context, orderId, cancellationToken),
            BotCommands.PayOrder when data.GuidArg(0) is { } orderId =>
                orders.PayOrderAsync(context, orderId, cancellationToken),
            BotCommands.AdminOrder when data.GuidArg(0) is { } orderId =>
                admin.ShowOrderAsync(context, orderId, cancellationToken),
            BotCommands.AdminAdvance when data.GuidArg(0) is { } orderId && TryOrderStatus(data.IntArg(1, -1), out var status) =>
                admin.AdvanceStatusAsync(context, orderId, status, cancellationToken),
            _ => account.ShowMenuAsync(context, cancellationToken)
        };

    private static bool TryPaymentMethod(int value, out PaymentMethod method)
    {
        method = (PaymentMethod)value;
        return Enum.IsDefined(method);
    }

    private static bool TryOrderStatus(int value, out OrderStatus status)
    {
        status = (OrderStatus)value;
        return Enum.IsDefined(status);
    }

    private static BotContext? ToContext(Update update)
    {
        if (update.CallbackQuery is { } callbackQuery)
        {
            var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From.Id;
            return new BotContext(chatId, callbackQuery.From.Id, callbackQuery.From.FirstName, callbackQuery.Message?.MessageId, callbackQuery.Id);
        }

        if (update.Message is { From: { } from } message)
        {
            return new BotContext(message.Chat.Id, from.Id, from.FirstName, MessageId: null, CallbackQueryId: null);
        }

        return null;
    }
}
