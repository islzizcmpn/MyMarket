namespace PcMarket.Application.Abstractions.Caching;

/// <summary>Typed distributed cache (backed by Redis) used for hot catalog reads, guest carts,
/// and other short-lived data.</summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Returns the cached value, or computes it with <paramref name="factory"/>, caches it, and returns it.</summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);
}
