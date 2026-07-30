using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PcMarket.Api.Auth;
using PcMarket.Api.Endpoints;
using PcMarket.Api.Middleware;
using PcMarket.Api.Realtime;
using PcMarket.Infrastructure.Persistence.Seed;
using Scalar.AspNetCore;
using Serilog;

namespace PcMarket.Api.Extensions;

/// <summary>Composes the HTTP pipeline and maps the baseline endpoints.</summary>
public static class ApiPipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // Must precede anything that reads the client address — request logging, the rate limiter — or every
        // request looks like it came from Nginx and they all share one rate-limit partition.
        app.UseForwardedHeaders();

        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseCors(ApiServiceExtensions.ClientsCorsPolicy);
        app.UseRateLimiter();

        // Ahead of output caching and the endpoints, so a cached or handled response is produced in the
        // language the caller asked for.
        app.UseRequestLocalization();
        app.UseOutputCache();

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = static async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
                });
                await context.Response.WriteAsync(payload);
            }
        });

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthorizationFilter()]
        });

        app.MapApiEndpoints();
        return app;
    }

    private static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

        app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok", service = "pcmarket-api" }));

        app.MapCatalogEndpoints();
        app.MapAuthEndpoints();
        app.MapCartEndpoints();
        app.MapUserEndpoints();
        app.MapOrderEndpoints();
        app.MapPaymentEndpoints();
        app.MapAdminEndpoints();
        app.MapMediaEndpoints();
        app.MapContentEndpoints();
        app.MapBotEndpoints();

        app.MapHub<OrderStatusHub>("/hubs/orders");
        app.MapHub<AdminOrderHub>("/hubs/admin");
    }

    /// <summary>Applies migrations and seeds baseline data at startup, unless that has been gated off.
    ///
    /// Auto-migrating on boot is convenient for development and a single-container compose stack, but on a
    /// rolling deploy several instances would race to migrate. Set <c>Database:MigrateOnStartup=false</c>
    /// there and run the schema change as its own step first — <c>dotnet PcMarket.Api.dll --migrate</c>
    /// against the same image (see <see cref="MigrateAndSeedAsync"/>).</summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        var migrateOnStartup = app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment());
        if (!migrateOnStartup)
        {
            app.Logger.LogInformation(
                "Startup migration is disabled (Database:MigrateOnStartup=false); expecting the schema to be current.");
            return;
        }

        await app.MigrateAndSeedAsync();
    }

    /// <summary>Applies pending migrations and seeds baseline data. Invoked at startup, or on its own via the
    /// <c>--migrate</c> switch so a deploy can gate the schema change ahead of rolling the app containers.</summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        var seedDemoCatalog = app.Configuration.GetValue("Database:SeedDemoCatalog", app.Environment.IsDevelopment());

        try
        {
            await DbSeeder.SeedAsync(app.Services, seedDemoCatalog);
            app.Logger.LogInformation("Database migrated and seeded (demo catalog: {SeedDemoCatalog}).", seedDemoCatalog);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Database migration/seeding failed.");
            throw;
        }
    }
}
