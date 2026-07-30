namespace PcMarket.Bot.Handlers;

/// <summary>Everything a flow needs to answer one Telegram update: who sent it, where to reply, and — for
/// button presses — which message to edit in place and which callback query to acknowledge.</summary>
/// <param name="ChatId">Chat to reply in.</param>
/// <param name="TelegramUserId">Sender's Telegram user id; also the key for conversation state and linking.</param>
/// <param name="FirstName">Sender's first name, for greetings.</param>
/// <param name="MessageId">The message carrying the pressed button, when this update is a callback query.</param>
/// <param name="CallbackQueryId">Callback query to acknowledge, when this update is a button press.</param>
public sealed record BotContext(
    long ChatId,
    long TelegramUserId,
    string? FirstName,
    int? MessageId,
    string? CallbackQueryId)
{
    /// <summary>True when the update came from an inline button, so replies can edit the message in place.</summary>
    public bool IsCallback => CallbackQueryId is not null;

    /// <summary>Cart token used while the Telegram user has no linked account.</summary>
    public string GuestCartToken => $"tg{TelegramUserId}";
}
