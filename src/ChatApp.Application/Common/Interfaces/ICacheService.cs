namespace ChatApp.Application.Common.Interfaces;

/// <summary>
/// [SOLID - Interface Segregation] Cache service interface nhỏ, đặc biệt.
/// Infrastructure implement bằng Redis, có thể swap sang MemoryCache dễ dàng.
///
/// [Design Pattern - Strategy] Swap implementation mà không thay code caller.
/// </summary>
public interface ICacheService
{
    /// <summary>T: kiểu data cần lưu (User, List&lt;MessageDto&gt;, v.v.)</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// [Cache-Aside Pattern] Get from cache, nếu miss thì execute factory và cache lại.
    /// Tránh code trùng lặp ở mỗi handler.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;
}

/// <summary>
/// Cache key constants để tránh magic strings rải rác trong codebase.
/// [Design Pattern - Constants Object] Centralize magic strings.
/// </summary>
public static class CacheKeys
{
    public static string OnlineUsers() => "online_users";
    public static string RoomMessages(Guid roomId) => $"room:{roomId}:messages";
    public static string UserByUsername(string username) => $"user:username:{username}";
    public static string UserById(Guid userId) => $"user:{userId}";
    public static string PublicRooms() => "rooms:public";
}
