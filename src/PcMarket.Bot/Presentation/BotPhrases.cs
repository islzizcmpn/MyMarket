using System.Globalization;
using PcMarket.Contracts.Orders;

namespace PcMarket.Bot.Presentation;

/// <summary>Every phrase the bot writes itself, keyed by <see cref="Phrase"/>. Product and category names are
/// <em>not</em> here — those come from the database through the catalog services, which translate them from the
/// culture the update is handled under.</summary>
public enum Phrase
{
    MenuCatalog,
    MenuSearch,
    MenuCart,
    MenuOrders,
    MenuLink,
    MenuLanguage,
    BackToMenu,
    BackToCategories,
    BackToCart,
    AddToCart,
    OpenInStore,
    CheckoutButton,
    PayNowButton,
    CancelOrderButton,
    ManageOrderButton,
    SharePhoneButton,

    WelcomeNamed,
    Welcome,
    Help,
    MainMenuGuest,
    MainMenuSignedIn,

    ProductBrand,
    ProductPrice,
    ProductInStock,
    ProductOutOfStock,
    ProductSpecs,

    CartTitle,
    CartEmpty,
    CartSubtotal,
    AddedToCartToast,

    OrderTitle,
    OrderStatusLabel,
    OrderPaymentLabel,
    OrderPlacedLabel,
    OrderTotalLabel,
    OrderDeliveryLabel,
    OrdersEmpty,
    OrdersPick,

    AlertNewOrder,
    AlertTotal,
    AlertPayment,
    AlertStatus,
    AlertCustomer,
    AlertItems,
    AlertDeliverTo,
    AlertLocation,

    AlreadyLinked,
    LinkPromptShareOnly,
    LinkPromptWithTyping,
    InvalidPhone,
    UseShareButton,
    OtpSent,
    OtpSms,
    OtpRejected,
    OtpInvalid,
    AccountGone,
    LinkedSuccess,
    Unlinked,
    NotLinked,

    CatalogEmpty,
    CatalogPick,
    CategoryEmpty,
    CategoryCount,
    ProductGone,
    SearchPrompt,
    SearchNoResults,
    SearchResults,

    CheckoutNeedsAccount,
    CheckoutHeader,
    ShareLocationButton,
    LocationNeeded,
    LocationReceived,
    DeliveringTo,
    OrderPlacedHeader,
    OrderPlacedPayHeader,
    LinkToPlaceOrder,
    LinkToSeeOrders,
    LinkToViewOrder,
    LinkToManageOrders,
    LinkToPayOrders,
    OrderCancelledToast,
    TapPayNow,

    AdminOrderNotFound,
    AdminDenied,
    AdminStatusToast,
    ChatId,

    GenericError,

    LanguagePanel,
    LanguageChanged,
    LanguageChangedToast,

    Currency,

    StatusCreated,
    StatusAwaitingPayment,
    StatusPaid,
    StatusProcessing,
    StatusShipped,
    StatusDelivered,
    StatusCancelled,
    StatusRefunded,

    PayStatusNone,
    PayStatusPending,
    PayStatusPaid,
    PayStatusFailed,
    PayStatusRefunded,

    MethodCash,
    MethodClick,
    MethodPayme,
    MethodUzcard,
    MethodHumo
}

/// <summary>The bot's phrase book. Wording for order statuses, payment methods and shared nouns matches the
/// storefront's resource files, so a customer who uses both sees the same words in both.</summary>
public static class BotPhrases
{
    /// <summary>One phrase in every language the bot speaks.</summary>
    private readonly record struct Translation(string Ru, string Uz, string En);

    public static string Get(string? culture, Phrase phrase)
    {
        var translation = Table[phrase];
        return BotLanguages.Normalize(culture) switch
        {
            "uz" => translation.Uz,
            "en" => translation.En,
            _ => translation.Ru
        };
    }

    /// <summary>Formats a phrase's placeholders. Arguments are formatted invariantly — they are ids, counts and
    /// already-rendered strings, never culture-sensitive numbers.</summary>
    public static string Format(string? culture, Phrase phrase, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(culture, phrase), args);

    public static string OrderStatusName(string? culture, OrderStatus status) => Get(culture, status switch
    {
        OrderStatus.Created => Phrase.StatusCreated,
        OrderStatus.AwaitingPayment => Phrase.StatusAwaitingPayment,
        OrderStatus.Paid => Phrase.StatusPaid,
        OrderStatus.Processing => Phrase.StatusProcessing,
        OrderStatus.Shipped => Phrase.StatusShipped,
        OrderStatus.Delivered => Phrase.StatusDelivered,
        OrderStatus.Cancelled => Phrase.StatusCancelled,
        _ => Phrase.StatusRefunded
    });

    public static string PaymentStatusName(string? culture, PaymentStatus status) => Get(culture, status switch
    {
        PaymentStatus.None => Phrase.PayStatusNone,
        PaymentStatus.Pending => Phrase.PayStatusPending,
        PaymentStatus.Paid => Phrase.PayStatusPaid,
        PaymentStatus.Failed => Phrase.PayStatusFailed,
        _ => Phrase.PayStatusRefunded
    });

    public static string PaymentMethodName(string? culture, PaymentMethod method) => Get(culture, method switch
    {
        PaymentMethod.Cash => Phrase.MethodCash,
        PaymentMethod.Click => Phrase.MethodClick,
        PaymentMethod.Payme => Phrase.MethodPayme,
        PaymentMethod.Uzcard => Phrase.MethodUzcard,
        _ => Phrase.MethodHumo
    });

    private static readonly IReadOnlyDictionary<Phrase, Translation> Table = new Dictionary<Phrase, Translation>
    {
        [Phrase.MenuCatalog] = new("🗂 Каталог", "🗂 Katalog", "🗂 Catalog"),
        [Phrase.MenuSearch] = new("🔎 Поиск", "🔎 Qidiruv", "🔎 Search"),
        [Phrase.MenuCart] = new("🛒 Корзина", "🛒 Savat", "🛒 Cart"),
        [Phrase.MenuOrders] = new("📦 Мои заказы", "📦 Buyurtmalarim", "📦 My orders"),
        [Phrase.MenuLink] = new("🔗 Привязать аккаунт", "🔗 Hisobni bog‘lash", "🔗 Link my account"),
        [Phrase.MenuLanguage] = new("🌐 Язык", "🌐 Til", "🌐 Language"),
        [Phrase.BackToMenu] = new("⬅️ Меню", "⬅️ Menyu", "⬅️ Menu"),
        [Phrase.BackToCategories] = new("⬅️ Категории", "⬅️ Bo‘limlar", "⬅️ Categories"),
        [Phrase.BackToCart] = new("⬅️ Корзина", "⬅️ Savat", "⬅️ Cart"),
        [Phrase.AddToCart] = new("➕ В корзину · {0}", "➕ Savatga · {0}", "➕ Add to cart · {0}"),
        [Phrase.OpenInStore] = new("🌐 Открыть в магазине", "🌐 Do‘konda ochish", "🌐 Open in store"),
        [Phrase.CheckoutButton] = new("✅ Оформить заказ", "✅ Buyurtma berish", "✅ Checkout"),
        [Phrase.PayNowButton] = new("💳 Оплатить", "💳 To‘lash", "💳 Pay now"),
        [Phrase.CancelOrderButton] = new("✖️ Отменить заказ", "✖️ Buyurtmani bekor qilish", "✖️ Cancel order"),
        [Phrase.ManageOrderButton] = new("🔧 Управлять заказом", "🔧 Buyurtmani boshqarish", "🔧 Manage order"),
        [Phrase.SharePhoneButton] = new("📱 Отправить номер телефона", "📱 Telefon raqamimni yuborish", "📱 Share my phone number"),

        [Phrase.WelcomeNamed] = new(
            """
            <b>Добро пожаловать в PcMarket, {0}!</b>

            Смотрите каталог, добавляйте товары в корзину и оформляйте заказ — всё прямо здесь.
            Отправьте любой текст, чтобы найти товар.
            """,
            """
            <b>PcMarketga xush kelibsiz, {0}!</b>

            Katalogni ko‘ring, mahsulotlarni savatga qo‘shing va buyurtma bering — barchasi shu yerda.
            Mahsulot qidirish uchun istalgan matnni yuboring.
            """,
            """
            <b>Welcome to PcMarket, {0}!</b>

            Browse the catalog, add items to your cart, and check out — all from here.
            Send any text to search for a product.
            """),
        [Phrase.Welcome] = new(
            """
            <b>Добро пожаловать в PcMarket!</b>

            Смотрите каталог, добавляйте товары в корзину и оформляйте заказ — всё прямо здесь.
            Отправьте любой текст, чтобы найти товар.
            """,
            """
            <b>PcMarketga xush kelibsiz!</b>

            Katalogni ko‘ring, mahsulotlarni savatga qo‘shing va buyurtma bering — barchasi shu yerda.
            Mahsulot qidirish uchun istalgan matnni yuboring.
            """,
            """
            <b>Welcome to PcMarket!</b>

            Browse the catalog, add items to your cart, and check out — all from here.
            Send any text to search for a product.
            """),
        [Phrase.Help] = new(
            """
            <b>Бот PcMarket</b>

            /catalog — категории каталога
            /search — поиск товаров
            /cart — ваша корзина
            /orders — заказы и их статус
            /language — сменить язык
            /link — привязать аккаунт PcMarket
            /unlink — отвязать этот Telegram
            /help — это сообщение

            Подсказка: просто отправьте текст, чтобы найти товар.
            """,
            """
            <b>PcMarket boti</b>

            /catalog — katalog bo‘limlari
            /search — mahsulot qidirish
            /cart — savatingiz
            /orders — buyurtmalar va ularning holati
            /language — tilni o‘zgartirish
            /link — PcMarket hisobingizni bog‘lash
            /unlink — bu Telegramni uzish
            /help — shu xabar

            Maslahat: mahsulot qidirish uchun shunchaki matn yuboring.
            """,
            """
            <b>PcMarket bot</b>

            /catalog — browse categories
            /search — search products
            /cart — view your cart
            /orders — your orders and their status
            /language — change the language
            /link — link your PcMarket account
            /unlink — unlink this Telegram account
            /help — this message

            Tip: just send any text to search.
            """),
        [Phrase.MainMenuGuest] = new(
            "<b>Главное меню</b>\n\nВы вошли как гость. Привяжите аккаунт, чтобы оформить заказ.",
            "<b>Asosiy menyu</b>\n\nSiz mehmon sifatida ko‘ryapsiz. Buyurtma berish uchun hisobingizni bog‘lang.",
            "<b>Main menu</b>\n\nYou are browsing as a guest. Link your account to check out."),
        [Phrase.MainMenuSignedIn] = new(
            "<b>Главное меню</b>\n\nВы вошли как {0}.",
            "<b>Asosiy menyu</b>\n\n{0} sifatida kirdingiz.",
            "<b>Main menu</b>\n\nSigned in as {0}."),

        [Phrase.ProductBrand] = new("Бренд", "Brend", "Brand"),
        [Phrase.ProductPrice] = new("Цена", "Narxi", "Price"),
        [Phrase.ProductInStock] = new("В наличии", "Sotuvda bor", "In stock"),
        [Phrase.ProductOutOfStock] = new("Нет в наличии", "Sotuvda yo‘q", "Out of stock"),
        [Phrase.ProductSpecs] = new("<b>Характеристики</b>", "<b>Xususiyatlari</b>", "<b>Specs</b>"),

        [Phrase.CartTitle] = new("<b>Ваша корзина</b>", "<b>Savatingiz</b>", "<b>Your cart</b>"),
        [Phrase.CartEmpty] = new(
            "<b>Ваша корзина</b>\n\nКорзина пуста. Загляните в каталог, чтобы добавить товар.",
            "<b>Savatingiz</b>\n\nSavat bo‘sh. Mahsulot qo‘shish uchun katalogni ko‘ring.",
            "<b>Your cart</b>\n\nYour cart is empty. Browse the catalog to add something."),
        [Phrase.CartSubtotal] = new("Итого", "Jami", "Subtotal"),
        [Phrase.AddedToCartToast] = new("Добавлено в корзину", "Savatga qo‘shildi", "Added to cart"),

        [Phrase.OrderTitle] = new("<b>Заказ {0}</b>", "<b>{0} buyurtmasi</b>", "<b>Order {0}</b>"),
        [Phrase.OrderStatusLabel] = new("Статус", "Holati", "Status"),
        [Phrase.OrderPaymentLabel] = new("Оплата", "To‘lov", "Payment"),
        [Phrase.OrderPlacedLabel] = new("Оформлен", "Berilgan", "Placed"),
        [Phrase.OrderTotalLabel] = new("Итого", "Jami", "Total"),
        [Phrase.OrderDeliveryLabel] = new("Доставка", "Yetkazib berish", "Delivery"),
        [Phrase.OrdersEmpty] = new(
            "<b>Ваши заказы</b>\n\nВы ещё не оформляли заказов.",
            "<b>Buyurtmalaringiz</b>\n\nSiz hali buyurtma bermagansiz.",
            "<b>Your orders</b>\n\nYou have not placed any orders yet."),
        [Phrase.OrdersPick] = new(
            "<b>Ваши заказы</b>\n\nВыберите заказ, чтобы посмотреть детали и статус.",
            "<b>Buyurtmalaringiz</b>\n\nTafsilotlar va holatni ko‘rish uchun buyurtmani tanlang.",
            "<b>Your orders</b>\n\nPick an order to see its details and status."),

        [Phrase.AlertNewOrder] = new("<b>🛒 Новый заказ {0}</b>", "<b>🛒 Yangi buyurtma {0}</b>", "<b>🛒 New order {0}</b>"),
        [Phrase.AlertTotal] = new("Итого", "Jami", "Total"),
        [Phrase.AlertPayment] = new("Оплата", "To‘lov", "Payment"),
        [Phrase.AlertStatus] = new("Статус", "Holati", "Status"),
        [Phrase.AlertCustomer] = new("Покупатель", "Xaridor", "Customer"),
        [Phrase.AlertItems] = new("Позиций", "Mahsulotlar", "Items"),
        [Phrase.AlertDeliverTo] = new("Доставить", "Manzil", "Deliver to"),
        [Phrase.AlertLocation] = new("На карте", "Xaritada", "On the map"),

        [Phrase.AlreadyLinked] = new(
            "Этот чат уже привязан к <b>{0}</b>.\nОтправьте /unlink, чтобы отвязать.",
            "Bu chat allaqachon <b>{0}</b> raqamiga bog‘langan.\nUzish uchun /unlink yuboring.",
            "This chat is already linked to <b>{0}</b>.\nSend /unlink to disconnect it."),
        [Phrase.LinkPromptShareOnly] = new(
            "Нажмите кнопку ниже, чтобы отправить номер телефона — это всё, что нужно.",
            "Telefon raqamingizni yuborish uchun quyidagi tugmani bosing — shuning o‘zi kifoya.",
            "Tap the button below to share your phone number — that is all it takes."),
        [Phrase.LinkPromptWithTyping] = new(
            "Нажмите кнопку ниже, чтобы отправить номер телефона — так привязка произойдёт сразу.\n\n" +
            "Можно ввести его вручную (например, <code>+998901234567</code>), но тогда нам придётся отправить " +
            "SMS с кодом для подтверждения.",
            "Telefon raqamingizni yuborish uchun quyidagi tugmani bosing — shunda bog‘lash darhol amalga oshadi.\n\n" +
            "Raqamni qo‘lda ham yozishingiz mumkin (masalan, <code>+998901234567</code>), lekin unda tasdiqlash " +
            "uchun SMS kod yuborishimizga to‘g‘ri keladi.",
            "Tap the button below to share your phone number — that links you straight away.\n\n" +
            "You can also type it (for example <code>+998901234567</code>), but then we have to send you an " +
            "SMS code to confirm it."),
        [Phrase.InvalidPhone] = new(
            "Это не похоже на номер телефона. Попробуйте ещё раз, например <code>+998901234567</code>.",
            "Bu telefon raqamiga o‘xshamaydi. Qayta urinib ko‘ring, masalan <code>+998901234567</code>.",
            "That does not look like a phone number. Try again, for example <code>+998901234567</code>."),
        [Phrase.UseShareButton] = new(
            "Пожалуйста, воспользуйтесь кнопкой <b>📱 Отправить номер телефона</b> ниже — мы можем привязать " +
            "только номер, который подтверждает Telegram.",
            "Iltimos, quyidagi <b>📱 Telefon raqamimni yuborish</b> tugmasidan foydalaning — biz faqat Telegram " +
            "tasdiqlagan raqamni bog‘lay olamiz.",
            "Please use the <b>📱 Share my phone number</b> button below — we can only link a number that " +
            "Telegram confirms for us."),
        [Phrase.OtpSent] = new(
            "Мы отправили 6-значный код на <b>{0}</b>. Отправьте его сюда, чтобы завершить привязку.",
            "<b>{0}</b> raqamiga 6 xonali kod yubordik. Bog‘lashni yakunlash uchun uni shu yerga yuboring.",
            "We sent a 6-digit code to <b>{0}</b>. Send it here to finish linking."),
        [Phrase.OtpSms] = new(
            "Код привязки Telegram PcMarket: {0}",
            "PcMarket Telegram bog‘lash kodi: {0}",
            "PcMarket Telegram link code: {0}"),
        [Phrase.OtpRejected] = new(
            "❌ {0} Отправьте код ещё раз или /link, чтобы начать заново.",
            "❌ {0} Kodni qayta yuboring yoki qaytadan boshlash uchun /link yuboring.",
            "❌ {0} Send the code again, or /link to restart."),
        [Phrase.OtpInvalid] = new(
            "❌ Неверный или просроченный код. Отправьте /link, чтобы начать заново.",
            "❌ Kod noto‘g‘ri yoki muddati o‘tgan. Qaytadan boshlash uchun /link yuboring.",
            "❌ Invalid or expired code. Send /link to start again."),
        [Phrase.AccountGone] = new(
            "❌ Такого аккаунта больше нет. Отправьте /link, чтобы начать заново.",
            "❌ Bunday hisob endi mavjud emas. Qaytadan boshlash uchun /link yuboring.",
            "❌ That account no longer exists. Send /link to start again."),
        [Phrase.LinkedSuccess] = new(
            "✅ Аккаунт <b>{0}</b> привязан. Корзина перенесена.",
            "✅ <b>{0}</b> raqamiga bog‘landi. Savatingiz saqlab qolindi.",
            "✅ Linked to <b>{0}</b>. Your cart carried over."),
        [Phrase.Unlinked] = new(
            "Этот Telegram больше не привязан.",
            "Bu Telegram hisobi endi bog‘lanmagan.",
            "This Telegram account is no longer linked."),
        [Phrase.NotLinked] = new(
            "Этот Telegram не был привязан.",
            "Bu Telegram hisobi bog‘lanmagan edi.",
            "This Telegram account was not linked."),

        [Phrase.CatalogEmpty] = new("Каталог пока пуст.", "Hozircha katalog bo‘sh.", "The catalog is empty right now."),
        [Phrase.CatalogPick] = new(
            "<b>Каталог</b>\n\nВыберите категорию:",
            "<b>Katalog</b>\n\nBo‘limni tanlang:",
            "<b>Catalog</b>\n\nPick a category:"),
        [Phrase.CategoryEmpty] = new(
            "В этой категории пока нет товаров.",
            "Bu bo‘limda hozircha mahsulot yo‘q.",
            "No products in this category yet."),
        [Phrase.CategoryCount] = new(
            "Товаров: {0} · страница {1}",
            "Mahsulotlar: {0} · {1}-sahifa",
            "{0} product(s) · page {1}"),
        [Phrase.ProductGone] = new(
            "Этот товар больше недоступен.",
            "Bu mahsulot endi mavjud emas.",
            "That product is no longer available."),
        [Phrase.SearchPrompt] = new(
            "🔎 Что вы ищете? Отправьте название товара или ключевое слово.",
            "🔎 Nimani qidiryapsiz? Mahsulot nomi yoki kalit so‘z yuboring.",
            "🔎 What are you looking for? Send me a product name or keyword."),
        [Phrase.SearchNoResults] = new(
            "По запросу <b>{0}</b> ничего не найдено. Попробуйте другое слово или загляните в каталог.",
            "<b>{0}</b> bo‘yicha hech narsa topilmadi. Boshqa so‘z bilan urinib ko‘ring yoki katalogni ko‘ring.",
            "Nothing found for <b>{0}</b>. Try another keyword or browse the catalog."),
        [Phrase.SearchResults] = new(
            "<b>Результаты по запросу «{0}»</b>\n\nНайдено: {1}",
            "<b>«{0}» bo‘yicha natijalar</b>\n\nTopildi: {1}",
            "<b>Results for “{0}”</b>\n\n{1} match(es):"),

        [Phrase.CheckoutNeedsAccount] = new(
            "Чтобы оформить заказ, сначала привяжите аккаунт PcMarket — корзина сохранится.",
            "Buyurtma berish uchun avval PcMarket hisobingizni bog‘lang — savat saqlanib qoladi.",
            "To check out, link your PcMarket account first — your cart carries over."),
        [Phrase.CheckoutHeader] = new(
            "<b>Оформление заказа</b>\n\nИтого: <b>{0}</b>\n\nОтправьте, пожалуйста, вашу <b>геолокацию</b> — " +
            "по ней курьер вас найдёт. Нажмите кнопку ниже.",
            "<b>Buyurtma berish</b>\n\nJami: <b>{0}</b>\n\nIltimos, <b>joylashuvingizni</b> yuboring — kuryer " +
            "sizni shu orqali topadi. Quyidagi tugmani bosing.",
            "<b>Checkout</b>\n\nTotal: <b>{0}</b>\n\nPlease send your <b>location</b> — that is how the courier " +
            "finds you. Tap the button below."),
        [Phrase.ShareLocationButton] = new(
            "📍 Отправить геолокацию",
            "📍 Joylashuvni yuborish",
            "📍 Send my location"),
        [Phrase.LocationNeeded] = new(
            "Чтобы оформить заказ, нужна геолокация. Нажмите кнопку <b>📍 Отправить геолокацию</b> ниже " +
            "(в Telegram: скрепка → Геопозиция).",
            "Buyurtma berish uchun joylashuv kerak. Quyidagi <b>📍 Joylashuvni yuborish</b> tugmasini bosing " +
            "(Telegramda: qisqich → Joylashuv).",
            "We need your location to place the order. Tap <b>📍 Send my location</b> below (in Telegram: " +
            "the paperclip → Location)."),
        [Phrase.LocationReceived] = new(
            "📍 Геолокация получена.\n\nТеперь напишите <b>номер дома и квартиры</b> (например, <code>12, кв. 5</code>).",
            "📍 Joylashuv qabul qilindi.\n\nEndi <b>uy va xonadon raqamini</b> yozing (masalan, <code>12, 5-xonadon</code>).",
            "📍 Location received.\n\nNow send your <b>house and flat number</b> (for example <code>12, flat 5</code>)."),
        [Phrase.DeliveringTo] = new(
            "Доставка: <b>{0}</b>\n\nКак хотите оплатить?",
            "Yetkazib berish: <b>{0}</b>\n\nQanday to‘lamoqchisiz?",
            "Delivering to: <b>{0}</b>\n\nHow would you like to pay?"),
        [Phrase.OrderPlacedHeader] = new(
            "✅ <b>Заказ оформлен!</b>\n\n",
            "✅ <b>Buyurtma qabul qilindi!</b>\n\n",
            "✅ <b>Order placed!</b>\n\n"),
        [Phrase.OrderPlacedPayHeader] = new(
            "✅ <b>Заказ оформлен!</b> Нажмите <b>Оплатить</b>, чтобы завершить оплату.\n\n",
            "✅ <b>Buyurtma qabul qilindi!</b> To‘lovni yakunlash uchun <b>To‘lash</b> tugmasini bosing.\n\n",
            "✅ <b>Order placed!</b> Tap <b>Pay now</b> to complete the payment.\n\n"),
        [Phrase.LinkToPlaceOrder] = new(
            "Привяжите аккаунт, чтобы оформить заказ.",
            "Buyurtma berish uchun hisobingizni bog‘lang.",
            "Link your account to place an order."),
        [Phrase.LinkToSeeOrders] = new(
            "Привяжите аккаунт PcMarket, чтобы видеть свои заказы.",
            "Buyurtmalaringizni ko‘rish uchun PcMarket hisobingizni bog‘lang.",
            "Link your PcMarket account to see your orders."),
        [Phrase.LinkToViewOrder] = new(
            "Привяжите аккаунт, чтобы просматривать заказы.",
            "Buyurtmalarni ko‘rish uchun hisobingizni bog‘lang.",
            "Link your account to view orders."),
        [Phrase.LinkToManageOrders] = new(
            "Привяжите аккаунт, чтобы управлять заказами.",
            "Buyurtmalarni boshqarish uchun hisobingizni bog‘lang.",
            "Link your account to manage orders."),
        [Phrase.LinkToPayOrders] = new(
            "Привяжите аккаунт, чтобы оплачивать заказы.",
            "Buyurtmalarni to‘lash uchun hisobingizni bog‘lang.",
            "Link your account to pay for orders."),
        [Phrase.OrderCancelledToast] = new("Заказ отменён", "Buyurtma bekor qilindi", "Order cancelled"),
        [Phrase.TapPayNow] = new(
            "Нажмите <b>Оплатить</b>, чтобы открыть страницу оплаты.\n\n",
            "To‘lov sahifasini ochish uchun <b>To‘lash</b> tugmasini bosing.\n\n",
            "Tap <b>Pay now</b> to open the payment page.\n\n"),

        [Phrase.AdminOrderNotFound] = new("Заказ не найден.", "Buyurtma topilmadi.", "Order not found."),
        [Phrase.AdminDenied] = new(
            "Для этого нужен привязанный аккаунт менеджера.",
            "Buning uchun bog‘langan menejer hisobi kerak.",
            "You need a linked manager account to do that."),
        [Phrase.AdminStatusToast] = new("Статус → {0}", "Holati → {0}", "Status → {0}"),
        [Phrase.ChatId] = new(
            "ID этого чата: <code>{0}</code>\n\nУкажите его в <code>TELEGRAM_ADMIN_CHAT_ID</code>, чтобы сюда " +
            "приходили новые заказы.",
            "Ushbu chat ID: <code>{0}</code>\n\nYangi buyurtmalar shu yerga tushishi uchun uni " +
            "<code>TELEGRAM_ADMIN_CHAT_ID</code> ga yozing.",
            "This chat's id is <code>{0}</code>\n\nPut it in <code>TELEGRAM_ADMIN_CHAT_ID</code> to receive new " +
            "orders here."),

        [Phrase.GenericError] = new(
            "На нашей стороне что-то пошло не так. Попробуйте ещё раз.",
            "Bizning tomonda nimadir noto‘g‘ri ketdi. Qayta urinib ko‘ring.",
            "Something went wrong on our side. Please try again."),

        [Phrase.LanguagePanel] = new(
            "<b>Язык</b>\n\nВыберите язык, на котором будет говорить бот.",
            "<b>Til</b>\n\nBot qaysi tilda gapirishini tanlang.",
            "<b>Language</b>\n\nPick the language you want the bot to speak."),
        [Phrase.LanguageChanged] = new(
            "✅ Язык изменён на <b>{0}</b>.",
            "✅ Til <b>{0}</b> ga o‘zgartirildi.",
            "✅ Language set to <b>{0}</b>."),
        [Phrase.LanguageChangedToast] = new("Язык обновлён", "Til yangilandi", "Language updated"),

        [Phrase.Currency] = new("сум", "so‘m", "UZS"),

        [Phrase.StatusCreated] = new("Создан", "Yaratildi", "Created"),
        [Phrase.StatusAwaitingPayment] = new("Ожидает оплаты", "To‘lov kutilmoqda", "Awaiting payment"),
        [Phrase.StatusPaid] = new("Оплачен", "To‘landi", "Paid"),
        [Phrase.StatusProcessing] = new("В обработке", "Tayyorlanmoqda", "Processing"),
        [Phrase.StatusShipped] = new("Отправлен", "Jo‘natildi", "Shipped"),
        [Phrase.StatusDelivered] = new("Доставлен", "Yetkazib berildi", "Delivered"),
        [Phrase.StatusCancelled] = new("Отменён", "Bekor qilindi", "Cancelled"),
        [Phrase.StatusRefunded] = new("Возврат оформлен", "Pul qaytarildi", "Refunded"),

        [Phrase.PayStatusNone] = new("Не требуется", "Talab qilinmaydi", "Not required"),
        [Phrase.PayStatusPending] = new("Ожидается", "Kutilmoqda", "Pending"),
        [Phrase.PayStatusPaid] = new("Оплачено", "To‘landi", "Paid"),
        [Phrase.PayStatusFailed] = new("Ошибка оплаты", "To‘lov amalga oshmadi", "Failed"),
        [Phrase.PayStatusRefunded] = new("Возвращено", "Qaytarildi", "Refunded"),

        [Phrase.MethodCash] = new("Наличными при получении", "Yetkazib berishda naqd pul", "Cash on delivery"),
        [Phrase.MethodClick] = new("Click", "Click", "Click"),
        [Phrase.MethodPayme] = new("Payme", "Payme", "Payme"),
        [Phrase.MethodUzcard] = new("Uzcard", "Uzcard", "Uzcard"),
        [Phrase.MethodHumo] = new("Humo", "Humo", "Humo")
    };
}
