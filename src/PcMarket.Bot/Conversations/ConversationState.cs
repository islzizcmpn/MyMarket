namespace PcMarket.Bot.Conversations;

/// <summary>What the bot is currently waiting for from this chat. Anything else is treated as a free-text
/// product search.</summary>
public enum BotStage
{
    None = 0,
    AwaitingPhone = 1,
    AwaitingOtp = 2,
    AwaitingSearch = 3,

    /// <summary>Checkout is waiting for a map pin. Values 4 and 5 belonged to the typed city and street steps
    /// this replaced; they are left unused so a conversation held in Redis when the bot was updated falls
    /// through to a search rather than into some other step.</summary>
    AwaitingLocation = 6,

    /// <summary>Checkout is waiting for the house and flat number that the pin cannot give.</summary>
    AwaitingHouse = 7
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

    /// <summary>Delivery pin shared during checkout.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>House and flat number typed after the pin — the part of an address a map cannot supply.</summary>
    public string? House { get; init; }
}
