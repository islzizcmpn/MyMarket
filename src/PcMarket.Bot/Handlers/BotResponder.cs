using Microsoft.Extensions.Logging;
using PcMarket.Bot.Runtime;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PcMarket.Bot.Handlers;

/// <summary>Sends the bot's replies. Button presses edit the originating message in place (so the chat does
/// not fill up with menus) and fall back to a fresh message when the edit is rejected — typically because
/// the rendered text is unchanged.</summary>
public sealed class BotResponder(TelegramClientAccessor accessor, ILogger<BotResponder> logger)
{
    public bool IsConfigured => accessor.IsConfigured;

    public async Task ReplyAsync(
        BotContext context,
        string html,
        InlineKeyboardMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        if (!accessor.IsConfigured)
        {
            logger.LogInformation("[Telegram:disabled] → chat {ChatId}: {Text}", context.ChatId, html);
            return;
        }

        if (context.IsCallback && context.MessageId is { } messageId)
        {
            try
            {
                await accessor.Client.EditMessageText(
                    context.ChatId,
                    messageId,
                    html,
                    ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (ApiRequestException ex)
            {
                logger.LogDebug(ex, "Editing message {MessageId} failed; sending a new message instead.", messageId);
            }
        }

        await SendAsync(context.ChatId, html, keyboard, cancellationToken);
    }

    public async Task SendAsync(
        long chatId,
        string html,
        IReplyMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        if (!accessor.IsConfigured)
        {
            logger.LogInformation("[Telegram:disabled] → chat {ChatId}: {Text}", chatId, html);
            return;
        }

        try
        {
            await accessor.Client.SendMessage(
                chatId,
                html,
                ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram send to chat {ChatId} failed.", chatId);
        }

        if (keyboard is null)
        {
            return;
        }

        // Telegram rejects the whole message when a single URL button is unreachable, so retry without the
        // buttons. A degraded card the user can read beats the silence they would otherwise get.
        try
        {
            await accessor.Client.SendMessage(chatId, html, ParseMode.Html, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram keyboard-less retry to chat {ChatId} also failed.", chatId);
        }
    }

    /// <summary>Acknowledges a button press, optionally with a toast. Telegram spins the button until this
    /// is called, so it runs for every callback — including ones that end in an error.</summary>
    public async Task AcknowledgeAsync(BotContext context, string? toast = null, CancellationToken cancellationToken = default)
    {
        if (!accessor.IsConfigured || context.CallbackQueryId is not { } callbackQueryId)
        {
            return;
        }

        try
        {
            await accessor.Client.AnswerCallbackQuery(callbackQueryId, toast, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            // Callback queries expire after ~15 minutes; a stale one is not worth failing the update over.
            logger.LogDebug(ex, "Answering callback query {CallbackQueryId} failed.", callbackQueryId);
        }
    }
}
