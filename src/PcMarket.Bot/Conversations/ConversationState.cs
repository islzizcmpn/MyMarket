namespace PcMarket.Bot.Conversations;

/// <summary>What the bot is currently waiting for from this chat. Anything else is treated as a free-text
/// product search.</summary>
public enum BotStage
{
    None = 0,
    AwaitingPhone = 1,
    AwaitingOtp = 2,
    AwaitingSearch = 3,
    AwaitingCity = 4,
    AwaitingStreet = 5
}

/// <summary>Per-Telegram-user conversation state, held in Redis with a short TTL. Deliberately small: it
/// carries only what a multi-step flow needs between updates (linking and checkout), never business data.</summary>
public sealed record ConversationState
{
    public static readonly ConversationState Empty = new();

    public BotStage Stage { get; init; } = BotStage.None;

    /// <summary>Phone being verified during account linking.</summary>
    public string? Phone { get; init; }

    /// <summary>True when the phone had no account and one was registered as part of linking, so the OTP
    /// must be verified through the auth service rather than the bot's own code.</summary>
    public bool PendingRegistration { get; init; }

    public string? Region { get; init; }

    public string? City { get; init; }

    public string? Street { get; init; }
}
