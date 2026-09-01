using ChatApp.Application.DTOs;

namespace ChatApp.Application.Common.Interfaces;

/// <summary>
/// [SOLID - Interface Segregation] Tách riêng event bus từ cache service.
/// [Design Pattern - Strategy] Kafka vs RabbitMQ implement cùng interface.
/// Application không biết mình đang dùng Kafka hay RabbitMQ.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish event lên message broker (Kafka hoặc RabbitMQ tùy config).
    /// </summary>
    Task PublishAsync<T>(T @event, string? topic = null, CancellationToken ct = default)
        where T : class;
}

/// <summary>
/// [Design Pattern - Notification Service Interface]
/// SignalR Hub implement interface này để Application không phụ thuộc trực tiếp vào SignalR.
/// </summary>
public interface IChatNotifier
{
    Task NotifyMessageSent(Guid roomId, MessageDto message, CancellationToken ct = default);
    Task NotifyUserJoined(Guid roomId, UserDto user, CancellationToken ct = default);
    Task NotifyUserLeft(Guid roomId, string username, CancellationToken ct = default);
    Task NotifyOnlineUsers(Guid roomId, IEnumerable<UserDto> users, CancellationToken ct = default);
}

/// <summary>
/// [Design Pattern - Current User Context]
/// Inject vào handlers để lấy thông tin user hiện tại từ HTTP context.
/// Tránh truyền userId qua parameter mọi nơi.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
}
