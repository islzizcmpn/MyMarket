using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Orders;
using PcMarket.Domain.Common;
using Telegram.Bot.Types.ReplyMarkups;
using DomainOrder = PcMarket.Domain.Ordering.Order;
using DomainStatus = PcMarket.Domain.Enums.OrderStatus;

namespace PcMarket.Bot.Presentation;

/// <summary>Builds the bot's inline keyboards. Every button payload goes through
/// <see cref="CallbackData.Of"/>, which enforces Telegram's 64-byte limit at build time. Labels come from
/// <see cref="BotPhrases"/> in the culture the update is being answered in.</summary>
public static class BotKeyboards
{
    public static InlineKeyboardMarkup MainMenu(string culture, bool isLinked)
    {
        List<List<InlineKeyboardButton>> rows =
        [
            [
                InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCatalog), BotCommands.Categories),
                InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuSearch), BotCommands.Search)
            ],
            [
                InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCart), BotCommands.Cart),
                InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuOrders), BotCommands.Orders)
            ]
        ];

        if (!isLinked)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuLink), BotCommands.Link)]);
        }

        rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuLanguage), BotCommands.Language)]);

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>The language panel. Labels are written in the language they select rather than translated, and
    /// the current one is ticked so the panel doubles as an answer to "which language am I in?".</summary>
    public static InlineKeyboardMarkup Languages(string culture)
    {
        var current = BotLanguages.Normalize(culture);
        var rows = BotLanguages.All
            .Select(language => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    language.Code == current ? $"✅ {language.Label}" : language.Label,
                    CallbackData.Of(BotCommands.SetLanguage, language.Code))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Categories(string culture, IReadOnlyList<CategoryNodeDto> categories)
    {
        var rows = categories
            .Select(category => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(category.Name, CallbackData.Of(BotCommands.Category, category.Id, 1))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup CategoryPage(
        string culture,
        CategoryNodeDto category,
        IReadOnlyList<ProductListItemDto> products,
        int page,
        bool hasNextPage)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        foreach (var child in category.Children)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData($"📁 {child.Name}", CallbackData.Of(BotCommands.Category, child.Id, 1))]);
        }

        foreach (var product in products)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(
                $"{product.Name} · {BotText.Money(culture, product.PriceFrom)}",
                CallbackData.Of(BotCommands.Product, product.Id))]);
        }

        var pager = new List<InlineKeyboardButton>();
        if (page > 1)
        {
            pager.Add(InlineKeyboardButton.WithCallbackData("◀️", CallbackData.Of(BotCommands.Category, category.Id, page - 1)));
        }

        if (hasNextPage)
        {
            pager.Add(InlineKeyboardButton.WithCallbackData("▶️", CallbackData.Of(BotCommands.Category, category.Id, page + 1)));
        }

        if (pager.Count > 0)
        {
            rows.Add(pager);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToCategories), BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCart), BotCommands.Cart)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup SearchResults(string culture, IReadOnlyList<ProductListItemDto> products)
    {
        var rows = products
            .Select(product => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{product.Name} · {BotText.Money(culture, product.PriceFrom)}",
                    CallbackData.Of(BotCommands.Product, product.Id))
            })
            .ToList();

        rows.Add([
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCatalog), BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Product(string culture, ProductDetailDto product, string storefrontUrl)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        foreach (var variant in product.Variants.Where(v => v.StockQty > 0))
        {
            var label = product.Variants.Count > 1
                ? $"➕ {variant.Sku} · {BotText.Money(culture, variant.Price)}"
                : BotPhrases.Format(culture, Phrase.AddToCart, BotText.Money(culture, variant.Price));
            rows.Add([InlineKeyboardButton.WithCallbackData(label, CallbackData.Of(BotCommands.AddToCart, variant.Id))]);
        }

        // Dropped rather than sent when the storefront is not publicly reachable: Telegram would reject the
        // whole product card over it, leaving the user with no Add-to-cart button at all.
        if (PublicUrl.IsReachableByTelegram(storefrontUrl))
        {
            rows.Add([InlineKeyboardButton.WithUrl(
                BotPhrases.Get(culture, Phrase.OpenInStore),
                $"{storefrontUrl.TrimEnd('/')}/product/{product.Slug}")]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCatalog), BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCart), BotCommands.Cart)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Cart(string culture, CartDto cart)
    {
        var rows = cart.Items
            .Select(item => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🗑 {item.ProductName}", CallbackData.Of(BotCommands.RemoveItem, item.Id))
            })
            .ToList();

        if (cart.Items.Count > 0)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.CheckoutButton), BotCommands.Checkout)]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuCatalog), BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Reply keyboard asking the customer to pin where they are. Telegram's own request-location
    /// button is the only route that yields real coordinates — a typed address cannot be turned into a pin
    /// without a geocoding service, and a pin is what actually gets a courier to the door.</summary>
    public static ReplyKeyboardMarkup RequestLocation(string culture) =>
        new(KeyboardButton.WithRequestLocation(BotPhrases.Get(culture, Phrase.ShareLocationButton)))
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

    public static InlineKeyboardMarkup PaymentMethods(string culture)
    {
        List<List<InlineKeyboardButton>> rows =
        [
            [InlineKeyboardButton.WithCallbackData(
                $"💵 {BotPhrases.PaymentMethodName(culture, PaymentMethod.Cash)}",
                CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Cash))],
            [InlineKeyboardButton.WithCallbackData(
                $"💳 {BotPhrases.PaymentMethodName(culture, PaymentMethod.Click)}",
                CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Click))],
            [InlineKeyboardButton.WithCallbackData(
                $"💳 {BotPhrases.PaymentMethodName(culture, PaymentMethod.Payme)}",
                CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Payme))],
            [InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToCart), BotCommands.Cart)]
        ];

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Orders(string culture, IReadOnlyList<OrderListItemDto> orders)
    {
        var rows = orders
            .Take(10)
            .Select(order => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(BotText.OrderButtonLabel(culture, order), CallbackData.Of(BotCommands.Order, order.Id))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup OrderDetail(string culture, OrderDto order, string? paymentUrl)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            // An unreachable gateway URL falls back to the callback button, which re-initiates payment, rather
            // than to a URL Telegram would reject along with the rest of the order card.
            var payLabel = BotPhrases.Get(culture, Phrase.PayNowButton);
            rows.Add(PublicUrl.IsReachableByTelegram(paymentUrl)
                ? [InlineKeyboardButton.WithUrl(payLabel, paymentUrl!)]
                : [InlineKeyboardButton.WithCallbackData(payLabel, CallbackData.Of(BotCommands.PayOrder, order.Id))]);
        }

        if (CanCancel(order.Status))
        {
            rows.Add([InlineKeyboardButton.WithCallbackData(
                BotPhrases.Get(culture, Phrase.CancelOrderButton),
                CallbackData.Of(BotCommands.CancelOrder, order.Id))]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.MenuOrders), BotCommands.Orders),
            InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Admin alert button: opens the order in the manager's own chat with the bot.</summary>
    public static InlineKeyboardMarkup AdminOrderAlert(string culture, Guid orderId) =>
        new(InlineKeyboardButton.WithCallbackData(
            BotPhrases.Get(culture, Phrase.ManageOrderButton),
            CallbackData.Of(BotCommands.AdminOrder, orderId)));

    /// <summary>Status buttons for an admin: exactly the transitions the order state machine allows next.</summary>
    public static InlineKeyboardMarkup AdminOrderActions(string culture, Guid orderId, OrderStatus status)
    {
        var rows = DomainOrder.AllowedFrom((DomainStatus)(int)status)
            .Select(next => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"➡️ {BotPhrases.OrderStatusName(culture, (OrderStatus)(int)next)}",
                    CallbackData.Of(BotCommands.AdminAdvance, orderId, (int)next))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData(BotPhrases.Get(culture, Phrase.BackToMenu), BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Reply keyboard asking the user to share their phone number for account linking.</summary>
    public static ReplyKeyboardMarkup RequestContact(string culture) =>
        new(KeyboardButton.WithRequestContact(BotPhrases.Get(culture, Phrase.SharePhoneButton)))
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

    private static bool CanCancel(OrderStatus status) =>
        DomainOrder.AllowedFrom((DomainStatus)(int)status).Contains(DomainStatus.Cancelled);
}
