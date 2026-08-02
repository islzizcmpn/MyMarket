using PcMarket.Application.Admin;
using PcMarket.Bot.Presentation;
using PcMarket.Domain.Common;
using PcMarket.Contracts.Orders;

namespace PcMarket.Bot.Handlers;

/// <summary>Back-office actions from the chat. Authorization comes from the *linked account's* roles, not
/// from the chat the alert was posted in — so forwarding an alert to someone else grants them nothing.
/// Status changes run through <see cref="AdminOrderService"/> and therefore the order state machine, which
/// keeps history, audit, and customer notifications identical to the admin panel.</summary>
public sealed class AdminFlow(AdminOrderService adminOrders, BotSession session, BotResponder responder)
{
    public async Task ShowOrderAsync(BotContext context, Guid orderId, CancellationToken cancellationToken = default)
    {
        if (!await IsStaffAsync(context, cancellationToken))
        {
            await DenyAsync(context, cancellationToken);
            return;
        }

        var detail = await adminOrders.GetAsync(orderId, cancellationToken);
        if (detail is null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Get(context.Culture, Phrase.AdminOrderNotFound),
                BotKeyboards.MainMenu(context.Culture, true),
                cancellationToken);
            return;
        }

        await responder.ReplyAsync(
            context,
            BotText.NewOrderAlert(context.Culture, detail.Order, detail.Customer?.Phone),
            BotKeyboards.AdminOrderActions(context.Culture, detail.Order.Id, detail.Order.Status),
            cancellationToken);
    }

    public async Task AdvanceStatusAsync(BotContext context, Guid orderId, OrderStatus status, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is null || !IsStaff(link.Roles))
        {
            await DenyAsync(context, cancellationToken);
            return;
        }

        var detail = await adminOrders.AdvanceStatusAsync(orderId, status, $"telegram:{link.Phone ?? link.UserId.ToString()}", cancellationToken);
        await responder.AcknowledgeAsync(
            context,
            BotPhrases.Format(context.Culture, Phrase.AdminStatusToast, BotPhrases.OrderStatusName(context.Culture, status)),
            cancellationToken);
        await responder.ReplyAsync(
            context,
            BotText.NewOrderAlert(context.Culture, detail.Order, detail.Customer?.Phone),
            BotKeyboards.AdminOrderActions(context.Culture, detail.Order.Id, detail.Order.Status),
            cancellationToken);
    }

    private async Task<bool> IsStaffAsync(BotContext context, CancellationToken cancellationToken)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        return link is not null && IsStaff(link.Roles);
    }

    private static bool IsStaff(IReadOnlyList<string> roles) =>
        roles.Contains(Roles.Admin) || roles.Contains(Roles.Manager);

    private Task DenyAsync(BotContext context, CancellationToken cancellationToken) =>
        responder.ReplyAsync(
            context,
            BotPhrases.Get(context.Culture, Phrase.AdminDenied),
            BotKeyboards.MainMenu(context.Culture, true),
            cancellationToken);
}
