using PcMarket.Api.Extensions;
using PcMarket.Application;
using PcMarket.Bot;
using PcMarket.Infrastructure;
using PcMarket.Payments;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPayments(builder.Configuration);
    // After AddInfrastructure: the bot's live Telegram messenger supersedes Infrastructure's no-op.
    builder.Services.AddBot(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();

    // Deploy-time schema step: apply migrations with the same image the app runs from, then exit, so a
    // rolling restart never has several instances racing to migrate. See docs/runbooks/deploy.md.
    if (args.Contains("--migrate"))
    {
        await app.MigrateAndSeedAsync();
        return;
    }

    app.UseApiPipeline();
    app.UsePaymentJobs();
    await app.SeedDatabaseAsync();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so integration tests can drive the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program { }
