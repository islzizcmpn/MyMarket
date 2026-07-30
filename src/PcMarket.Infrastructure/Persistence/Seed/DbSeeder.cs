using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Localization;
using PcMarket.Domain.Common;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Infrastructure.Persistence.Seed;

/// <summary>Applies pending migrations and seeds baseline data: roles, an initial admin user, and a demo
/// catalog. The catalog source is <c>SEED_CATALOG_PATH</c> (an external JSON file, any environment) when set;
/// otherwise, when <paramref name="seedDemoCatalog"/> is true (Development), the demo catalog embedded in this
/// assembly. Catalog import is idempotent, so it is safe to run on every startup.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        bool seedDemoCatalog = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<PcMarketDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(sp);
        await SeedAdminAsync(sp);
        await SeedCatalogAsync(db, seedDemoCatalog, cancellationToken);

        if (seedDemoCatalog)
        {
            await SeedContentAsync(db, cancellationToken);
            await SeedTranslationsAsync(db, cancellationToken);
        }
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var adminPhone = Environment.GetEnvironmentVariable("SEED_ADMIN_PHONE") ?? "+998900000000";

        if (await userManager.FindByNameAsync(adminPhone) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = adminPhone,
            PhoneNumber = adminPhone,
            PhoneNumberConfirmed = true,
            FullName = "Administrator"
        };

        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? "Admin!23456";
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed admin user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
    }

    private static async Task SeedCatalogAsync(PcMarketDbContext db, bool seedDemoCatalog, CancellationToken cancellationToken)
    {
        // Explicit external file wins, in any environment.
        var path = Environment.GetEnvironmentVariable("SEED_CATALOG_PATH");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await using var fileStream = File.OpenRead(path);
            await CatalogImporter.ImportAsync(db, fileStream, cancellationToken);
            return;
        }

        if (!seedDemoCatalog)
        {
            return;
        }

        await using var embedded = OpenEmbeddedDemoCatalog();
        if (embedded is not null)
        {
            await CatalogImporter.ImportAsync(db, embedded, cancellationToken);
        }
    }

    private static async Task SeedContentAsync(PcMarketDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Banners.AnyAsync(cancellationToken))
        {
            db.Banners.Add(new Domain.Content.Banner
            {
                Title = "Back-to-work deals",
                Subtitle = "Save on laptops, components and accessories this week.",
                ImageUrl = "https://placehold.co/1200x360/3b3ba6/3b3ba6",
                LinkUrl = "/catalog",
                SortOrder = 0
            });
            db.Banners.Add(new Domain.Content.Banner
            {
                Title = "Genuine brands, fast delivery",
                Subtitle = "ASUS · Logitech · Kingston and more.",
                ImageUrl = "https://placehold.co/1200x360/ff7a45/ff7a45",
                LinkUrl = "/catalog",
                SortOrder = 1
            });
        }

        if (!await db.CmsBlocks.AnyAsync(b => b.Key == "home-intro", cancellationToken))
        {
            db.CmsBlocks.Add(new Domain.Content.CmsBlock
            {
                Key = "home-intro",
                Title = "Welcome to PCMarket",
                Body = "Uzbekistan's PC & electronics store — genuine gear, Click/Payme/Uzcard/Humo payments, and cash on delivery."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Adds Russian and Uzbek text for the demo categories and content. English is not stored here —
    /// it is the canonical value on the entity itself and the fallback when a translation is missing. Matched
    /// by slug/key and skipped when already present, so it is safe on every startup.</summary>
    private static async Task SeedTranslationsAsync(PcMarketDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Select(c => new { c.Id, c.Slug })
            .ToDictionaryAsync(c => c.Slug, c => c.Id, cancellationToken);

        foreach (var (slug, ru, uz) in DemoCategoryTranslations)
        {
            if (categories.TryGetValue(slug, out var id))
            {
                Add(TranslatableEntities.Category, id, nameof(Domain.Catalog.Category.Name), ru, uz);
            }
        }

        var banners = await db.Banners
            .Select(b => new { b.Id, b.Title })
            .ToListAsync(cancellationToken);

        foreach (var (title, ruTitle, uzTitle, ruSubtitle, uzSubtitle) in DemoBannerTranslations)
        {
            var banner = banners.FirstOrDefault(b => b.Title == title);
            if (banner is not null)
            {
                Add(TranslatableEntities.Banner, banner.Id, nameof(Domain.Content.Banner.Title), ruTitle, uzTitle);
                Add(TranslatableEntities.Banner, banner.Id, nameof(Domain.Content.Banner.Subtitle), ruSubtitle, uzSubtitle);
            }
        }

        var intro = await db.CmsBlocks.FirstOrDefaultAsync(b => b.Key == "home-intro", cancellationToken);
        if (intro is not null)
        {
            Add(TranslatableEntities.CmsBlock, intro.Id, nameof(Domain.Content.CmsBlock.Title),
                "Добро пожаловать в PCMarket",
                "PCMarket'ga xush kelibsiz");
            Add(TranslatableEntities.CmsBlock, intro.Id, nameof(Domain.Content.CmsBlock.Body),
                "Магазин ПК и электроники в Узбекистане — оригинальная техника, оплата Click/Payme/Uzcard/Humo и наличными при получении.",
                "O‘zbekistondagi kompyuter va elektronika do‘koni — original texnika, Click/Payme/Uzcard/Humo orqali va yetkazib berishda naqd to‘lov.");
        }

        await db.SaveChangesAsync(cancellationToken);

        void Add(string entityType, Guid entityId, string field, string ru, string uz)
        {
            foreach (var (culture, value) in new[] { ("ru", ru), ("uz", uz) })
            {
                var exists = db.ContentTranslations.Local.Any(t =>
                                 t.EntityType == entityType && t.EntityId == entityId
                                 && t.Field == field && t.Culture == culture)
                             || db.ContentTranslations.Any(t =>
                                 t.EntityType == entityType && t.EntityId == entityId
                                 && t.Field == field && t.Culture == culture);

                if (!exists)
                {
                    db.ContentTranslations.Add(new Domain.Content.ContentTranslation
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        Field = field,
                        Culture = culture,
                        Value = value
                    });
                }
            }
        }
    }

    private static readonly (string Slug, string Ru, string Uz)[] DemoCategoryTranslations =
    [
        ("computers", "Компьютеры", "Kompyuterlar"),
        ("laptops", "Ноутбуки", "Noutbuklar"),
        ("accessories", "Аксессуары", "Aksessuarlar"),
        ("mice", "Мыши", "Sichqonchalar"),
        ("memory", "Память", "Xotira"),
    ];

    private static readonly (string Title, string RuTitle, string UzTitle, string RuSubtitle, string UzSubtitle)[]
        DemoBannerTranslations =
        [
            ("Back-to-work deals",
                "Скидки к началу рабочего сезона",
                "Ish mavsumi chegirmalari",
                "Скидки на ноутбуки, комплектующие и аксессуары на этой неделе.",
                "Shu hafta noutbuklar, komplektuvchilar va aksessuarlarga chegirmalar."),
            ("Genuine brands, fast delivery",
                "Оригинальные бренды, быстрая доставка",
                "Original brendlar, tez yetkazib berish",
                "ASUS · Logitech · Kingston и другие.",
                "ASUS · Logitech · Kingston va boshqalar."),
        ];

    private static Stream? OpenEmbeddedDemoCatalog()
    {
        var assembly = typeof(DbSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("demo-catalog.json", StringComparison.OrdinalIgnoreCase));
        return resourceName is null ? null : assembly.GetManifestResourceStream(resourceName);
    }
}
