using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Admin;
using PcMarket.Bot.Configuration;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Presentation;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.Bot.Notifications;

/// <summary>Posts every new order to the configured admin chat with a button that opens the order's
/// management view. Runs alongside the SignalR admin feed rather than replacing it: managers get the alert
/// wherever they are, and act on it without opening the panel.</summary>
public sealed class TelegramAdminAlertHandler(
    AdminOrderService adminOrders,
    BotResponder responder,
    IUserLanguageStore userLanguages,
    IOptions<TelegramSettings> settings,
    ILogger<TelegramAdminAlertHandler> logger) : IDomainEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (settings.Value.AdminChatId is not { } adminChatId || !responder.IsConfigured)
        {
            return;
        }

        var detail = await adminOrders.GetAsync(domainEvent.OrderId, cancellationToken);
        if (detail is null)
        {
            logger.LogWarning("New-order alert skipped: order {OrderId} not found.", domainEvent.OrderId);
            return;
        }

        // A private chat's id is the manager's own Telegram user id, so their chosen language is on the account
        // behind it. A group or channel belongs to nobody in particular and gets the bot's default.
        var culture = BotLanguages.Normalize(
            await userLanguages.GetByTelegramUserIdAsync(adminChatId, cancellationToken));

        await responder.SendAsync(
            adminChatId,
            BotText.NewOrderAlert(culture, detail.Order, detail.Customer?.Phone),
            BotKeyboards.AdminOrderAlert(culture, detail.Order.Id),
            cancellationToken);
    }
}
