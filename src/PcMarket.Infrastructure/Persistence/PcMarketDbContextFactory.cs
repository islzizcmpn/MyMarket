using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PcMarket.Infrastructure.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without the API host.
/// Reads the connection string from <c>POSTGRES_CONNECTION</c>, falling back to a local dev default.</summary>
public class PcMarketDbContextFactory : IDesignTimeDbContextFactory<PcMarketDbContext>
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=pcmarket;Username=pcmarket;Password=pcmarket";

    public PcMarketDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<PcMarketDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(PcMarketDbContextFactory).Assembly.FullName))
            .Options;

        return new PcMarketDbContext(options);
    }
}
