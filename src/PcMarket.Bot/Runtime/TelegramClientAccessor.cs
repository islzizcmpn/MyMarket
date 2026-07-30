using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcMarket.Bot.Configuration;
using Telegram.Bot;

namespace PcMarket.Bot.Runtime;

/// <summary>Owns the single <see cref="ITelegramBotClient"/> for the process. Registered even when the bot
/// is switched off — in that case <see cref="IsConfigured"/> is false and callers skip their sends, so the
/// API boots and runs identically with or without a bot token.</summary>
public sealed class TelegramClientAccessor
{
    private readonly ITelegramBotClient? _client;

    public TelegramClientAccessor(IOptions<TelegramSettings> options, ILogger<TelegramClientAccessor> logger)
    {
        Settings = options.Value;

        if (!Settings.Enabled || string.IsNullOrWhiteSpace(Settings.BotToken))
        {
            logger.LogInformation("Telegram bot is disabled (no token configured); outbound Telegram messages are no-ops.");
            return;
        }

        try
        {
            _client = new TelegramBotClient(Settings.BotToken);
        }
        catch (Exception ex)
        {
            // A malformed token must not take the whole host down — degrade to disabled.
            logger.LogError(ex, "Telegram bot token is invalid; the bot will stay disabled.");
        }
    }

    public TelegramSettings Settings { get; }

    public bool IsConfigured => _client is not null;

    /// <exception cref="InvalidOperationException">If no valid bot token is configured.</exception>
    public ITelegramBotClient Client =>
        _client ?? throw new InvalidOperationException("Telegram bot is not configured.");
}
