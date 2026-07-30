using System.Text.Json;
using PcMarket.Application.Abstractions.Caching;
using StackExchange.Redis;

namespace PcMarket.Infrastructure.Caching;

/// <summary>Redis-backed <see cref="ICacheService"/>. Values are stored as JSON.</summary>
public sealed class RedisCacheService(IConnectionMultiplexer connection) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _db = connection.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(value, JsonOptions);
        return _db.StringSetAsync(key, payload, ttl);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _db.KeyDeleteAsync(key);

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }
}
