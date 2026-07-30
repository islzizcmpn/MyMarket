using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Messaging;
using Telegram.Bot;

namespace PcMarket.Bot.Runtime;

/// <summary>Live <see cref="ITelegramMessenger"/> over the bot client. Registered after Infrastructure's
/// no-op so it wins resolution wherever the bot module is wired up. Sends plain text (no parse mode) so
/// callers outside the bot never have to escape markup.</summary>
public sealed class TelegramMessenger(TelegramClientAccessor accessor, ILogger<TelegramMessenger> logger) : ITelegramMessenger
{
    public bool IsConfigured => accessor.IsConfigured;

    public async Task<bool> SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!accessor.IsConfigured)
        {
            logger.LogInformation("[Telegram:disabled] → chat {ChatId}: {Text}", chatId, text);
            return false;
        }

        try
        {
            await accessor.Client.SendMessage(chatId, text, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram send to chat {ChatId} failed.", chatId);
            return false;
        }
    }
}
