using System.Globalization;
using Microsoft.Extensions.Logging;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Localization;
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
    LanguageFlow language,
    BotLanguageService languages,
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

        context = context with
        {
            Culture = await languages.ResolveAsync(context.TelegramUserId, context.TelegramLanguageCode, cancellationToken)
        };

        ApplyCulture(context.Culture);

        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                await responder.AcknowledgeAsync(context, toast: null, cancellationToken);

                var data = CallbackData.Parse(callbackQuery.Data);

                // Checkout and Link are the two buttons that *raise* a reply keyboard; clearing for them
                // would take down the one they are about to put up.
                if (data.Command is not (BotCommands.Checkout or BotCommands.Link))
                {
                    await LeavePromptKeyboardAsync(context, cancellationToken);
                }

                await HandleCallbackAsync(context, data, cancellationToken);
            }
            else if (update.Message is { } message)
            {
                await HandleMessageAsync(context, message, cancellationToken);
            }
        }
        catch (DomainException ex)
        {
            await responder.ReplyAsync(context, $"⚠️ {BotText.Escape(ex.Message)}", BotKeyboards.MainMenu(context.Culture, true), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handling Telegram update {UpdateId} failed.", update.Id);
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.GenericError),
                BotKeyboards.MainMenu(context.Culture, true),
                cancellationToken);
        }
    }

    /// <summary>Puts the chosen language on the ambient UI culture for the rest of this update, which is what
    /// the Application layer's <c>ILanguageContext</c> reads — so category and product names come back
    /// translated without every catalog call having to pass the language along.</summary>
    private void ApplyCulture(string culture)
    {
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException ex)
        {
            // Only reachable in a globalization-invariant host, where the bot's own phrases still translate
            // and only database-backed names fall back to English.
            logger.LogDebug(ex, "Culture {Culture} is unavailable in this runtime.", culture);
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

        if (message.Location is { } location)
        {
            // Only meaningful mid-checkout; a pin sent at any other time is ignored rather than treated as
            // an address for an order that does not exist yet.
            var stage = (await conversations.GetAsync(context.TelegramUserId, cancellationToken)).Stage;
            if (stage is BotStage.AwaitingLocation or BotStage.AwaitingHouse)
            {
                await orders.SetLocationAsync(context, location.Latitude, location.Longitude, cancellationToken);
            }
            else
            {
                // A pin with no checkout behind it means the button outlived its step — most often because the
                // conversation aged out of Redis while the keyboard stayed on screen. Take it down now.
                await responder.ClearReplyKeyboardAsync(context.ChatId, cancellationToken);
            }

            return;
        }

        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.StartsWith('/'))
        {
            // Commands may arrive as "/start@BotName" in groups.
            var command = text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
            var argument = text.Split(' ', 2) is [_, var rest] ? rest.Trim() : string.Empty;

            // /link is the one command that raises a reply keyboard of its own.
            if (command != "/link")
            {
                await LeavePromptKeyboardAsync(context, cancellationToken);
            }

            await HandleCommandAsync(context, command, argument, cancellationToken);
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
            case BotStage.AwaitingLocation:
                // Typing an address here is the obvious mistake to make, so it gets the button again rather
                // than being searched for as a product name.
                await orders.AskForLocationAsync(context, cancellationToken);
                break;
            case BotStage.AwaitingHouse:
                await orders.SetHouseAsync(context, text, cancellationToken);
                break;
            default:
                await catalog.SearchAsync(context, text, cancellationToken);
                break;
        }
    }

    /// <summary>Takes a share-contact or share-location keyboard down when the customer leaves the step that
    /// raised it by another door. Only these two stages hold one: by the time checkout reaches
    /// <see cref="BotStage.AwaitingHouse"/>, or linking reaches <see cref="BotStage.AwaitingOtp"/>, the
    /// message that moved them on has already removed it.
    ///
    /// Telegram keeps a reply keyboard up until it is told otherwise — <c>OneTimeKeyboard</c> only collapses
    /// it — so without this the button follows the customer around the bot long after the step is over.</summary>
    private async Task LeavePromptKeyboardAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        if (state.Stage is not (BotStage.AwaitingPhone or BotStage.AwaitingLocation))
        {
            return;
        }

        await conversations.SetAsync(context.TelegramUserId, state with { Stage = BotStage.None }, cancellationToken);
        await responder.ClearReplyKeyboardAsync(context.ChatId, cancellationToken);
    }

    private Task HandleCommandAsync(BotContext context, string command, string argument, CancellationToken cancellationToken) =>
        command switch
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
            "/language" => language.ShowPanelAsync(context, cancellationToken),
            "/link" => account.BeginLinkAsync(context, cancellationToken),
            "/unlink" => account.UnlinkAsync(context, cancellationToken),
            // Setup aid: a chat's id cannot be looked up from an invite link, and getUpdates is unavailable
            // while a webhook is registered, so the chat has to say its own id. Not a secret — knowing it
            // grants nothing, since admin actions are gated on the caller's linked roles.
            "/chatid" => responder.SendAsync(
                context.ChatId,
                BotPhrases.Format(context.Culture, Phrase.ChatId, context.ChatId),
                keyboard: null,
                cancellationToken),
            _ => account.ShowHelpAsync(context, cancellationToken)
        };

    private Task HandleCallbackAsync(BotContext context, CallbackData data, CancellationToken cancellationToken) =>
        data.Command switch
        {
            BotCommands.Home => account.ShowMenuAsync(context, cancellationToken),
            BotCommands.Link => account.BeginLinkAsync(context, cancellationToken),
            BotCommands.Language => language.ShowPanelAsync(context, cancellationToken),
            BotCommands.SetLanguage => language.SetLanguageAsync(context, data.Arg(0), cancellationToken),
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
            return new BotContext(
                chatId,
                callbackQuery.From.Id,
                callbackQuery.From.FirstName,
                callbackQuery.Message?.MessageId,
                callbackQuery.Id,
                callbackQuery.From.LanguageCode);
        }

        if (update.Message is { From: { } from } message)
        {
            return new BotContext(
                message.Chat.Id,
                from.Id,
                from.FirstName,
                MessageId: null,
                CallbackQueryId: null,
                from.LanguageCode);
        }

        return null;
    }
}
