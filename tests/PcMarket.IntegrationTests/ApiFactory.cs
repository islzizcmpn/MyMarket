using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Infrastructure.Persistence;
using PcMarket.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace PcMarket.IntegrationTests;

/// <summary>Boots the real API against throwaway PostgreSQL + Redis containers, wiring their connection
/// strings in through environment variables so the host picks them up at <c>CreateBuilder</c> time (the
/// host reads the connection string eagerly during service registration, before any later config source
/// would apply). Env vars are process-global, so the assembly disables test parallelization
/// (see <c>AssemblyInfo.cs</c>) to keep concurrent factories from clobbering each other.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Secret the Telegram webhook expects in <c>X-Telegram-Bot-Api-Secret-Token</c> under test.</summary>
    public const string WebhookSecret = "test-webhook-secret";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Redis__Configuration", _redis.GetConnectionString());

        // The bot is switched on so its webhook endpoint is mapped, but stays token-less: updates are
        // handled and their side effects land in the database, while every outbound send is a no-op.
        Environment.SetEnvironmentVariable("Telegram__Enabled", "true");
        Environment.SetEnvironmentVariable("Telegram__WebhookSecretToken", WebhookSecret);

        // Typed-phone linking ships disabled (it is the only route that costs an SMS), but the code path is
        // still supported and worth covering, so it is switched on here. Phase7BotPhoneEntryTests overrides it
        // back to false to cover the shipped default.
        Environment.SetEnvironmentVariable("Telegram__AllowPhoneEntry", "true");

        // Every TestServer request reports the same (absent) client address, so the whole suite would land in
        // one rate-limit partition and later tests would 429 on traffic a real client would never generate.
        // The limits themselves are not what these tests cover.
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__GlobalPermitLimit", "100000");

        // WebApplicationFactory intercepts the host build, so Program's post-build seeding never
        // runs under tests — apply migrations + baseline seed explicitly here.
        _ = Server;
        await DbSeeder.SeedAsync(Services);
        await SeedCatalogAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        Environment.SetEnvironmentVariable("Redis__Configuration", null);
        Environment.SetEnvironmentVariable("Telegram__Enabled", null);
        Environment.SetEnvironmentVariable("Telegram__WebhookSecretToken", null);
        Environment.SetEnvironmentVariable("Telegram__AllowPhoneEntry", null);
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", null);
        Environment.SetEnvironmentVariable("RateLimiting__GlobalPermitLimit", null);
    }

    public IServiceScope CreateScope() => Services.CreateScope();

    private async Task SeedCatalogAsync()
    {
        const string catalog = """
        {
          "brands": [ { "name": "ASUS", "slug": "asus" } ],
          "categories": [
            { "name": "Computers", "slug": "computers" },
            { "name": "Laptops", "slug": "laptops", "parentSlug": "computers" }
          ],
          "products": [
            { "name": "ASUS VivoBook 15", "slug": "asus-vivobook-15", "categorySlug": "laptops", "brandSlug": "asus",
              "description": "Everyday laptop", "specs": { "RAM": "16GB" },
              "variants": [ { "sku": "ASUS-VB15", "price": 7500000, "stockQty": 5 } ],
              "images": [ "https://example.com/a.jpg" ] }
          ]
        }
        """;

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PcMarketDbContext>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(catalog));
        await CatalogImporter.ImportAsync(db, stream);
    }
}
