using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Domain.Common;
using PcMarket.Infrastructure;
using PcMarket.Infrastructure.Identity;
using PcMarket.Infrastructure.Persistence;
using PcMarket.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace PcMarket.IntegrationTests;

/// <summary>Verifies the Phase 1 persistence slice against a real PostgreSQL: migration applies,
/// baseline roles/admin seed, and the JSON catalog importer inserts (and is idempotent).</summary>
public class CatalogPersistenceTests : IAsyncLifetime
{
    private const string DemoCatalog = """
    {
      "brands": [
        { "name": "ASUS", "slug": "asus" },
        { "name": "Logitech", "slug": "logitech" },
        { "name": "Kingston", "slug": "kingston" }
      ],
      "categories": [
        { "name": "Computers", "slug": "computers" },
        { "name": "Laptops", "slug": "laptops", "parentSlug": "computers" },
        { "name": "Accessories", "slug": "accessories" },
        { "name": "Mice", "slug": "mice", "parentSlug": "accessories" },
        { "name": "Memory", "slug": "memory", "parentSlug": "computers" }
      ],
      "products": [
        { "name": "ASUS VivoBook 15", "slug": "asus-vivobook-15", "categorySlug": "laptops", "brandSlug": "asus",
          "specs": { "RAM": "16GB" },
          "variants": [ { "sku": "ASUS-VB15", "price": 7500000, "stockQty": 12 } ],
          "images": [ "https://example.com/a.jpg" ] },
        { "name": "Logitech M330", "slug": "logitech-m330", "categorySlug": "mice", "brandSlug": "logitech",
          "variants": [ { "sku": "LOG-M330", "price": 320000, "stockQty": 40 } ] },
        { "name": "Kingston FURY 16GB", "slug": "kingston-fury-16", "categorySlug": "memory", "brandSlug": "kingston",
          "variants": [ { "sku": "KING-16", "price": 650000, "stockQty": 60 } ] }
      ]
    }
    """;

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(_db.GetConnectionString());
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Seeder_CreatesRolesAndAdmin()
    {
        await DbSeeder.SeedAsync(_provider);

        using var scope = _provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(role), $"role {role} should exist");
        }

        var admin = await userManager.FindByNameAsync("+998900000000");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin, Roles.Admin));
    }

    [Fact]
    public async Task Importer_InsertsCatalog_AndIsIdempotent()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PcMarketDbContext>();
        await db.Database.MigrateAsync();

        var first = await CatalogImporter.ImportAsync(db, ToStream(DemoCatalog));
        Assert.Equal(3, first.Brands);
        Assert.Equal(5, first.Categories);
        Assert.Equal(3, first.Products);

        // Re-importing the same document inserts nothing.
        var second = await CatalogImporter.ImportAsync(db, ToStream(DemoCatalog));
        Assert.Equal(0, second.Brands);
        Assert.Equal(0, second.Categories);
        Assert.Equal(0, second.Products);

        Assert.Equal(3, await db.ProductVariants.CountAsync());
        Assert.Equal(2, await db.Categories.CountAsync(c => c.ParentId == null));

        // JSONB round-trips.
        var vivobook = await db.Products.SingleAsync(p => p.Slug == "asus-vivobook-15");
        Assert.Equal("16GB", vivobook.Specs["RAM"]);
    }

    private static MemoryStream ToStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
