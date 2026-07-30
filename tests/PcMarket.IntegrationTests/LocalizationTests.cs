using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Admin;
using PcMarket.Application.Localization;
using PcMarket.Contracts.Admin;
using PcMarket.Contracts.Catalog;
using PcMarket.Contracts.Common;
using PcMarket.Contracts.Content;
using PcMarket.Domain.Content;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.IntegrationTests;

/// <summary>Database-backed text follows the caller's <c>Accept-Language</c>, falling back to the entity's own
/// (English) column when a translation is missing. Also pins the per-language cache key: the category tree is
/// translated before it is cached, so one shared key would serve the first caller's language to everyone.</summary>
public class LocalizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BlockKey = "localization-test-block";

    [Theory]
    [InlineData("ru", "Компьютеры")]
    [InlineData("uz", "Kompyuterlar")]
    [InlineData("en", "Computers")]
    [InlineData("ru-RU", "Компьютеры")]   // regional variants resolve to their base language
    [InlineData("de", "Computers")]       // unsupported languages fall back to English
    public async Task CategoryNames_FollowAcceptLanguage(string acceptLanguage, string expected)
    {
        await EnsureTranslationsAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);

        var categories = await client.GetFromJsonAsync<List<CategoryNodeDto>>("/api/v1/catalog/categories");

        var computers = Assert.Single(categories!, c => c.Slug == "computers");
        Assert.Equal(expected, computers.Name);
    }

    [Fact]
    public async Task CategoryNames_FallBackToEnglish_WhenNoLanguageRequested()
    {
        await EnsureTranslationsAsync();

        var categories = await factory.CreateClient()
            .GetFromJsonAsync<List<CategoryNodeDto>>("/api/v1/catalog/categories");

        var computers = Assert.Single(categories!, c => c.Slug == "computers");
        Assert.Equal("Computers", computers.Name);
    }

    /// <summary>Each language must get its own cached tree — the regression this guards against is one
    /// culture-blind cache key handing the first caller's language to every later caller.</summary>
    [Fact]
    public async Task CategoryTree_IsCachedPerLanguage()
    {
        await EnsureTranslationsAsync();

        async Task<string> NameIn(string language)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("Accept-Language", language);
            var categories = await client.GetFromJsonAsync<List<CategoryNodeDto>>("/api/v1/catalog/categories");
            return categories!.Single(c => c.Slug == "computers").Name;
        }

        // Русский first so it is the one that populates the cache, then confirm the others are unaffected.
        Assert.Equal("Компьютеры", await NameIn("ru"));
        Assert.Equal("Kompyuterlar", await NameIn("uz"));
        Assert.Equal("Computers", await NameIn("en"));
        Assert.Equal("Компьютеры", await NameIn("ru"));
    }

    [Fact]
    public async Task CmsBlock_FallsBackPerField_NotAllOrNothing()
    {
        await EnsureTranslationsAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "ru");

        var block = await client.GetFromJsonAsync<CmsBlockDto>($"/api/v1/content/blocks/{BlockKey}");

        // Only the title is translated, so the untranslated body must still come back in English rather than
        // the whole block reverting.
        Assert.Equal("Заголовок по-русски", block!.Title);
        Assert.Equal("English body", block.Body);
    }

    /// <summary>A back-office edit has to show up on the storefront straight away. The category tree is cached
    /// per language for ten minutes, so without invalidation the editor would see no change and reasonably
    /// conclude the save had failed. Uses "laptops" so it does not disturb the other tests' category.</summary>
    [Fact]
    public async Task AdminEdit_ShowsOnStorefront_WithoutWaitingForTheCacheToExpire()
    {
        await EnsureTranslationsAsync();

        // The seeder ships translations for the demo categories; warm the Russian tree so the edit below has
        // a stale cache entry to invalidate.
        Assert.Equal("Ноутбуки", await LaptopsNameIn("ru"));

        using (var scope = factory.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<AdminCatalogService>();
            var laptops = (await admin.ListCategoriesAsync()).Single(c => c.Slug == "laptops");

            await admin.SaveCategoryAsync(laptops.Id, new SaveCategoryRequest(
                laptops.Name, laptops.Slug, laptops.ParentId, laptops.SortOrder, laptops.IsActive,
                [new TranslationDto("Name", "ru", "Портативные компьютеры")]));
        }

        Assert.Equal("Портативные компьютеры", await LaptopsNameIn("ru"));
        Assert.Equal("Laptops", await LaptopsNameIn("en"));

        // Russian was the only language submitted, so the Uzbek row is dropped and that language falls back to
        // English. Clearing a box is how the back office reverts a translation.
        Assert.Equal("Laptops", await LaptopsNameIn("uz"));
    }

    private async Task<string> LaptopsNameIn(string language)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", language);
        var categories = await client.GetFromJsonAsync<List<CategoryNodeDto>>("/api/v1/catalog/categories");
        return categories!.SelectMany(c => c.Children).Single(c => c.Slug == "laptops").Name;
    }

    /// <summary>Adds the fixture's translated content once per test class; the factory seeds an untranslated
    /// catalog, and each test method gets a fresh instance of this class.</summary>
    private async Task EnsureTranslationsAsync()
    {
        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PcMarketDbContext>();

        var computers = await db.Categories.SingleAsync(c => c.Slug == "computers");
        if (!await db.ContentTranslations.AnyAsync(t => t.EntityId == computers.Id))
        {
            db.ContentTranslations.AddRange(
                new ContentTranslation
                {
                    EntityType = TranslatableEntities.Category,
                    EntityId = computers.Id,
                    Field = "Name",
                    Culture = "ru",
                    Value = "Компьютеры"
                },
                new ContentTranslation
                {
                    EntityType = TranslatableEntities.Category,
                    EntityId = computers.Id,
                    Field = "Name",
                    Culture = "uz",
                    Value = "Kompyuterlar"
                });
        }

        if (!await db.CmsBlocks.AnyAsync(b => b.Key == BlockKey))
        {
            var block = new CmsBlock { Key = BlockKey, Title = "English title", Body = "English body" };
            db.CmsBlocks.Add(block);
            db.ContentTranslations.Add(new ContentTranslation
            {
                EntityType = TranslatableEntities.CmsBlock,
                EntityId = block.Id,
                Field = "Title",
                Culture = "ru",
                Value = "Заголовок по-русски"
            });
        }

        await db.SaveChangesAsync();
    }
}
