using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Catalog;

namespace PcMarket.UnitTests.Bot;

/// <summary>Telegram validates URL buttons server-side and rejects the <em>whole</em> message when one is
/// unreachable, so a misconfigured storefront URL used to take the entire product card down with it — no
/// card meant no Add-to-cart button, which is what made products impossible to add from the bot.
/// The button must be dropped instead. See docs/issues/bot-product-click-no-cart/journal.md.</summary>
public class ProductKeyboardTests
{
    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("https://localhost")]
    [InlineData("http://127.0.0.1:5155")]
    [InlineData("http://admin.localhost")]
    [InlineData("http://nginx")]
    [InlineData("http://192.168.1.10:8080")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://172.16.4.1")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public void Product_OmitsTheStoreLink_WhenTelegramCouldNotReachIt(string storefrontUrl)
    {
        var labels = Labels(BotKeyboards.Product(Product(), storefrontUrl));

        Assert.DoesNotContain("🌐 Open in store", labels);
        // The card must still be usable — this is the whole point of dropping the button.
        Assert.Contains(labels, label => label.StartsWith("➕", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://pcmarket.uz")]
    [InlineData("https://known-down-bias-entertainment.trycloudflare.com")]
    [InlineData("http://shop.example.com:8080")]
    public void Product_KeepsTheStoreLink_WhenPubliclyReachable(string storefrontUrl)
    {
        Assert.Contains("🌐 Open in store", Labels(BotKeyboards.Product(Product(), storefrontUrl)));
    }

    [Fact]
    public void Product_OffersAddToCartOnlyForVariantsInStock()
    {
        var labels = Labels(BotKeyboards.Product(Product(stockQty: 0), "https://pcmarket.uz"));

        Assert.DoesNotContain(labels, label => label.StartsWith("➕", StringComparison.Ordinal));
    }

    private static List<string> Labels(Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup markup) =>
        markup.InlineKeyboard.SelectMany(row => row).Select(button => button.Text).ToList();

    private static ProductDetailDto Product(int stockQty = 7) =>
        new(
            Guid.NewGuid(),
            "Kingston FURY 16GB DDR4",
            "kingston-fury-16gb-ddr4",
            "Description",
            CategoryId: Guid.NewGuid(),
            BrandName: "Kingston",
            Specs: new Dictionary<string, string>(),
            Images: [],
            Variants:
            [
                new ProductVariantDto(Guid.NewGuid(), "KF-16", new Dictionary<string, string>(), 650_000m, null, stockQty)
            ]);
}
