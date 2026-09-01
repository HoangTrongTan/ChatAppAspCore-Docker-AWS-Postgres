using System.Text.Json;
using ChatApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChatApp.Infrastructure.Caching;

/// <summary>
/// [Redis Cache Service] Implement ICacheService bằng StackExchange.Redis.
///
/// [Design Pattern - Strategy] Application chỉ biết ICacheService interface.
/// Swap Redis → MemoryCache chỉ cần thay class này, không đụng Application.
///
/// [System Design - Caching Strategy]
/// - Read-through: Không implement ở đây (application tự quyết định cache khi nào)
/// - Cache-Aside: Application check cache trước, miss thì query DB → update cache
/// - Write-invalidate: Sau khi write → xóa cache key liên quan
///
/// [Redis Data Types dùng ở đây]
/// - STRING: lưu serialized JSON (user, room info)
/// - SET: lưu online users (Redis SET có O(1) SISMEMBER)
/// Production nên dùng thêm:
/// - HASH: lưu user presence với metadata
/// - SORTED SET: leaderboard, message ordering
/// </summary>
public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger
) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    // [Configuration] Default TTL - có thể move vào IOptions<CacheOptions>
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue) return null;

            // Deserialize JSON → T
            // Cast tường minh sang string: RedisValue có implicit conversion sang
            // cả string và byte[] nên gọi Deserialize<T>(value) trực tiếp bị ambiguous overload.
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            // [Resilience] Cache miss không nên làm crash app
            // Log warning và return null → caller sẽ fallback về DB
            logger.LogWarning(ex, "[Cache] GET failed for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, expiry ?? DefaultExpiry);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Cache] SET failed for key: {Key}", key);
            // Không throw → cache failure không ảnh hưởng main flow
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Cache] REMOVE failed for key: {Key}", key);
        }
    }

    /// <summary>
    /// [Cache-Aside Pattern Implementation]
    /// 1. Check Redis → hit → return
    /// 2. Miss → execute factory (DB query)
    /// 3. Set result in Redis → return
    ///
    /// [Concurrency] Có thể xảy ra "thundering herd": nhiều requests hit cache miss
    /// cùng lúc → tất cả query DB song song. Production fix: SemaphoreSlim per key
    /// hoặc Redis SETNX (set if not exists).
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            logger.LogDebug("[Cache] HIT for key: {Key}", key);
            return cached;
        }

        logger.LogDebug("[Cache] MISS for key: {Key}", key);

        var result = await factory();
        await SetAsync(key, result, expiry, ct);
        return result;
    }
}
