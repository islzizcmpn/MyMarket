using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using PcMarket.Infrastructure.Configuration;
using PcMarket.Infrastructure.Persistence;
using StackExchange.Redis;

namespace PcMarket.Api.Health;

/// <summary>Reports healthy when the primary database accepts a connection.</summary>
public sealed class PostgresHealthCheck(PcMarketDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL.");
    }
}

/// <summary>Reports healthy when Redis responds to PING.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed.", ex);
        }
    }
}

/// <summary>Reports healthy when MinIO answers a bucket-existence probe.</summary>
public sealed class MinioHealthCheck(IMinioClient client, IOptions<MinioSettings> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(options.Value.Bucket), cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO probe failed.", ex);
        }
    }
}
