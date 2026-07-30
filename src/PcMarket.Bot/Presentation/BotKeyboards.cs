using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Orders;
using PcMarket.Domain.Common;
using Telegram.Bot.Types.ReplyMarkups;
using DomainOrder = PcMarket.Domain.Ordering.Order;
using DomainStatus = PcMarket.Domain.Enums.OrderStatus;

namespace PcMarket.Bot.Presentation;

/// <summary>Builds the bot's inline keyboards. Every button payload goes through
/// <see cref="CallbackData.Of"/>, which enforces Telegram's 64-byte limit at build time.</summary>
public static class BotKeyboards
{
    public static InlineKeyboardMarkup MainMenu(bool isLinked)
    {
        List<List<InlineKeyboardButton>> rows =
        [
            [
                InlineKeyboardButton.WithCallbackData("🗂 Catalog", BotCommands.Categories),
                InlineKeyboardButton.WithCallbackData("🔎 Search", BotCommands.Search)
            ],
            [
                InlineKeyboardButton.WithCallbackData("🛒 Cart", BotCommands.Cart),
                InlineKeyboardButton.WithCallbackData("📦 My orders", BotCommands.Orders)
            ]
        ];

        if (!isLinked)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("🔗 Link my account", BotCommands.Link)]);
        }

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Categories(IReadOnlyList<CategoryNodeDto> categories)
    {
        var rows = categories
            .Select(category => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(category.Name, CallbackData.Of(BotCommands.Category, category.Id, 1))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup CategoryPage(
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
                $"{product.Name} · {BotText.Money(product.PriceFrom)}",
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
            InlineKeyboardButton.WithCallbackData("⬅️ Categories", BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData("🛒 Cart", BotCommands.Cart)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup SearchResults(IReadOnlyList<ProductListItemDto> products)
    {
        var rows = products
            .Select(product => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{product.Name} · {BotText.Money(product.PriceFrom)}",
                    CallbackData.Of(BotCommands.Product, product.Id))
            })
            .ToList();

        rows.Add([
            InlineKeyboardButton.WithCallbackData("🗂 Catalog", BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Product(ProductDetailDto product, string storefrontUrl)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        foreach (var variant in product.Variants.Where(v => v.StockQty > 0))
        {
            var label = product.Variants.Count > 1
                ? $"➕ {variant.Sku} · {BotText.Money(variant.Price)}"
                : $"➕ Add to cart · {BotText.Money(variant.Price)}";
            rows.Add([InlineKeyboardButton.WithCallbackData(label, CallbackData.Of(BotCommands.AddToCart, variant.Id))]);
        }

        // Dropped rather than sent when the storefront is not publicly reachable: Telegram would reject the
        // whole product card over it, leaving the user with no Add-to-cart button at all.
        if (PublicUrl.IsReachableByTelegram(storefrontUrl))
        {
            rows.Add([InlineKeyboardButton.WithUrl("🌐 Open in store", $"{storefrontUrl.TrimEnd('/')}/product/{product.Slug}")]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData("🗂 Catalog", BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData("🛒 Cart", BotCommands.Cart)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Cart(CartDto cart)
    {
        var rows = cart.Items
            .Select(item => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🗑 {item.ProductName}", CallbackData.Of(BotCommands.RemoveItem, item.Id))
            })
            .ToList();

        if (cart.Items.Count > 0)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("✅ Checkout", BotCommands.Checkout)]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData("🗂 Catalog", BotCommands.Categories),
            InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Regions()
    {
        var rows = new List<List<InlineKeyboardButton>>();
        for (var index = 0; index < UzbekistanRegions.All.Count; index += 2)
        {
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(UzbekistanRegions.All[index], CallbackData.Of(BotCommands.Region, index))
            };

            if (index + 1 < UzbekistanRegions.All.Count)
            {
                row.Add(InlineKeyboardButton.WithCallbackData(UzbekistanRegions.All[index + 1], CallbackData.Of(BotCommands.Region, index + 1)));
            }

            rows.Add(row);
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Cart", BotCommands.Cart)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup PaymentMethods()
    {
        List<List<InlineKeyboardButton>> rows =
        [
            [InlineKeyboardButton.WithCallbackData("💵 Cash on delivery", CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Cash))],
            [InlineKeyboardButton.WithCallbackData("💳 Click", CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Click))],
            [InlineKeyboardButton.WithCallbackData("💳 Payme", CallbackData.Of(BotCommands.PaymentMethod, (int)PaymentMethod.Payme))],
            [InlineKeyboardButton.WithCallbackData("⬅️ Cart", BotCommands.Cart)]
        ];

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup Orders(IReadOnlyList<OrderListItemDto> orders)
    {
        var rows = orders
            .Take(10)
            .Select(order => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(BotText.OrderButtonLabel(order), CallbackData.Of(BotCommands.Order, order.Id))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup OrderDetail(OrderDto order, string? paymentUrl)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            // An unreachable gateway URL falls back to the callback button, which re-initiates payment, rather
            // than to a URL Telegram would reject along with the rest of the order card.
            rows.Add(PublicUrl.IsReachableByTelegram(paymentUrl)
                ? [InlineKeyboardButton.WithUrl("💳 Pay now", paymentUrl!)]
                : [InlineKeyboardButton.WithCallbackData("💳 Pay now", CallbackData.Of(BotCommands.PayOrder, order.Id))]);
        }

        if (CanCancel(order.Status))
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("✖️ Cancel order", CallbackData.Of(BotCommands.CancelOrder, order.Id))]);
        }

        rows.Add([
            InlineKeyboardButton.WithCallbackData("📦 My orders", BotCommands.Orders),
            InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Admin alert button: opens the order in the manager's own chat with the bot.</summary>
    public static InlineKeyboardMarkup AdminOrderAlert(Guid orderId) =>
        new(InlineKeyboardButton.WithCallbackData("🔧 Manage order", CallbackData.Of(BotCommands.AdminOrder, orderId)));

    /// <summary>Status buttons for an admin: exactly the transitions the order state machine allows next.</summary>
    public static InlineKeyboardMarkup AdminOrderActions(Guid orderId, OrderStatus status)
    {
        var rows = DomainOrder.AllowedFrom((DomainStatus)(int)status)
            .Select(next => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"➡️ {next}", CallbackData.Of(BotCommands.AdminAdvance, orderId, (int)next))
            })
            .ToList();

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Menu", BotCommands.Home)]);
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Reply keyboard asking the user to share their phone number for account linking.</summary>
    public static ReplyKeyboardMarkup RequestContact() =>
        new(KeyboardButton.WithRequestContact("📱 Share my phone number"))
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

    private static bool CanCancel(OrderStatus status) =>
        DomainOrder.AllowedFrom((DomainStatus)(int)status).Contains(DomainStatus.Cancelled);
}
