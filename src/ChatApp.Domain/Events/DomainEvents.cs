using ChatApp.Domain.Common;

namespace ChatApp.Domain.Events;

/// <summary>
/// [Domain Event] Fired khi một message được gửi thành công.
/// Handlers của event này:
///   1. KafkaProducer → publish lên topic "chat-messages"
///   2. SignalR Hub → broadcast đến clients trong room
///   3. NotificationHandler → push notification qua RabbitMQ
/// </summary>
public record MessageSentEvent(
    Guid MessageId,
    string Content,
    Guid SenderId,
    Guid RoomId
) : BaseDomainEvent
{
    public override string EventType => nameof(MessageSentEvent);
}

/// <summary>
/// [Domain Event] Fired khi user được tạo mới.
/// </summary>
public record UserCreatedEvent(
    Guid UserId,
    string Username
) : BaseDomainEvent
{
    public override string EventType => nameof(UserCreatedEvent);
}

/// <summary>
/// [Domain Event] Fired khi user online/offline.
/// → SignalR broadcast presence update đến tất cả clients.
/// </summary>
public record UserStatusChangedEvent(
    Guid UserId,
    string Username,
    bool IsOnline
) : BaseDomainEvent
{
    public override string EventType => nameof(UserStatusChangedEvent);
}

/// <summary>
/// [Domain Event] Fired khi user join room.
/// → Notify các member hiện tại trong room.
/// </summary>
public record UserJoinedRoomEvent(
    Guid UserId,
    string Username,
    int AvatarIndex,
    Guid RoomId,
    string RoomName
) : BaseDomainEvent
{
    public override string EventType => nameof(UserJoinedRoomEvent);
}
