using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Messaging;

namespace PcMarket.Infrastructure.Messaging;

/// <summary>Stands in for the real Telegram messenger when the bot module is not wired up (tests, or hosts
/// that do not run the bot). <c>PcMarket.Bot</c> registers its live implementation after Infrastructure, so
/// the live one wins wherever the bot is present.</summary>
public sealed class NoOpTelegramMessenger(ILogger<NoOpTelegramMessenger> logger) : ITelegramMessenger
{
    public bool IsConfigured => false;

    public Task<bool> SendTextAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Telegram:noop] → chat {ChatId}: {Text}", chatId, text);
        return Task.FromResult(false);
    }
}
