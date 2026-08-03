namespace PcMarket.Bot.Presentation;

/// <summary>Inline-button callback commands. Telegram caps <c>callback_data</c> at 64 bytes, so payloads
/// are short prefixes plus ids — never slugs, whose length is unbounded.</summary>
public static class BotCommands
{
    public const string Home = "home";
    public const string Categories = "cats";

    /// <summary><c>c:{categoryId}:{page}</c></summary>
    public const string Category = "c";

    /// <summary><c>p:{productId}</c></summary>
    public const string Product = "p";

    /// <summary><c>a:{productVariantId}</c></summary>
    public const string AddToCart = "a";

    public const string Cart = "cart";

    /// <summary><c>x:{cartItemId}</c></summary>
    public const string RemoveItem = "x";

    public const string Checkout = "co";

    /// <summary><c>pm:{paymentMethodValue}</c></summary>
    public const string PaymentMethod = "pm";

    public const string Orders = "orders";

    /// <summary><c>o:{orderId}</c></summary>
    public const string Order = "o";

    /// <summary><c>ox:{orderId}</c></summary>
    public const string CancelOrder = "ox";

    /// <summary><c>pay:{orderId}</c></summary>
    public const string PayOrder = "pay";

    public const string Search = "search";
    public const string Link = "link";

    /// <summary>Opens the language panel.</summary>
    public const string Language = "lang";

    /// <summary><c>sl:{languageCode}</c></summary>
    public const string SetLanguage = "sl";

    /// <summary><c>ao:{orderId}</c></summary>
    public const string AdminOrder = "ao";

    /// <summary><c>as:{orderId}:{orderStatusValue}</c></summary>
    public const string AdminAdvance = "as";

    public const string Noop = "noop";
}

/// <summary>A parsed inline-button payload: a command plus colon-separated arguments.</summary>
public readonly record struct CallbackData(string Command, IReadOnlyList<string> Args)
{
    /// <summary>Telegram's hard limit on <c>callback_data</c>.</summary>
    public const int MaxLength = 64;

    public static CallbackData Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CallbackData(BotCommands.Noop, []);
        }

        var parts = raw.Split(':');
        return new CallbackData(parts[0], parts.Length > 1 ? parts[1..] : []);
    }

    /// <summary>Builds a payload, e.g. <c>Of("as", orderId, 4)</c>.</summary>
    /// <exception cref="ArgumentException">If the result would exceed Telegram's 64-byte limit.</exception>
    public static string Of(string command, params object[] args)
    {
        var data = args.Length == 0 ? command : $"{command}:{string.Join(':', args)}";
        return data.Length <= MaxLength
            ? data
            : throw new ArgumentException($"Callback data '{data}' exceeds {MaxLength} bytes.", nameof(args));
    }

    public string? Arg(int index) => index < Args.Count ? Args[index] : null;

    public Guid? GuidArg(int index) => Guid.TryParse(Arg(index), out var value) ? value : null;

    public int IntArg(int index, int fallback) => int.TryParse(Arg(index), out var value) ? value : fallback;
}
