using Microsoft.Extensions.Options;
using PcMarket.Application.Catalog;
using PcMarket.Bot.Configuration;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Catalog;

namespace PcMarket.Bot.Handlers;

/// <summary>Catalog browsing inside the chat: category tree, paged product lists, product detail, and
/// free-text search. Reads go straight through <see cref="CatalogService"/> — the same use-cases the web
/// storefront calls — so bot and web can never drift apart.</summary>
public sealed class CatalogFlow(
    CatalogService catalog,
    BotResponder responder,
    IConversationStore conversations,
    IOptions<TelegramSettings> settings)
{
    private readonly TelegramSettings _settings = settings.Value;

    public async Task ShowCategoriesAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var tree = await catalog.GetCategoryTreeAsync(cancellationToken);
        if (tree.Count == 0)
        {
            await responder.ReplyAsync(context, "The catalog is empty right now.", BotKeyboards.MainMenu(true), cancellationToken);
            return;
        }

        await responder.ReplyAsync(context, "<b>Catalog</b>\n\nPick a category:", BotKeyboards.Categories(tree), cancellationToken);
    }

    public async Task ShowCategoryAsync(BotContext context, Guid categoryId, int page, CancellationToken cancellationToken = default)
    {
        var tree = await catalog.GetCategoryTreeAsync(cancellationToken);
        var category = Find(tree, categoryId);
        if (category is null)
        {
            await ShowCategoriesAsync(context, cancellationToken);
            return;
        }

        var pageSize = _settings.PageSize;
        var products = await catalog.GetProductsAsync(
            category.Slug, brandSlug: null, minPrice: null, maxPrice: null,
            ProductSort.Newest, page, pageSize, cancellationToken);

        var hasNext = (long)page * pageSize < products.Total;
        var header = $"<b>{BotText.Escape(category.Name)}</b>\n\n" +
                     (products.Total == 0
                         ? "No products in this category yet."
                         : $"{products.Total} product(s) · page {page}");

        await responder.ReplyAsync(
            context,
            header,
            BotKeyboards.CategoryPage(category, products.Items, page, hasNext),
            cancellationToken);
    }

    public async Task ShowProductAsync(BotContext context, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await catalog.GetProductByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            await responder.ReplyAsync(context, "That product is no longer available.", BotKeyboards.MainMenu(true), cancellationToken);
            return;
        }

        await responder.ReplyAsync(
            context,
            BotText.Product(product),
            BotKeyboards.Product(product, _settings.StorefrontUrl),
            cancellationToken);
    }

    public async Task PromptSearchAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(context.TelegramUserId, state with { Stage = BotStage.AwaitingSearch }, cancellationToken);
        await responder.ReplyAsync(context, "🔎 What are you looking for? Send me a product name or keyword.", keyboard: null, cancellationToken);
    }

    public async Task SearchAsync(BotContext context, string term, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        if (state.Stage == BotStage.AwaitingSearch)
        {
            await conversations.SetAsync(context.TelegramUserId, state with { Stage = BotStage.None }, cancellationToken);
        }

        var results = await catalog.SearchAsync(term, page: 1, _settings.PageSize, cancellationToken);
        if (results.Items.Count == 0)
        {
            await responder.ReplyAsync(
                context,
                $"Nothing found for <b>{BotText.Escape(term)}</b>. Try another keyword or browse the catalog.",
                BotKeyboards.MainMenu(true),
                cancellationToken);
            return;
        }

        await responder.ReplyAsync(
            context,
            $"<b>Results for “{BotText.Escape(term)}”</b>\n\n{results.Total} match(es):",
            BotKeyboards.SearchResults(results.Items),
            cancellationToken);
    }

    private static CategoryNodeDto? Find(IReadOnlyList<CategoryNodeDto> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }

            var match = Find(node.Children, id);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
