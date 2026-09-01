using ChatApp.Domain.Common;
using ChatApp.Domain.Events;
using ChatApp.Domain.Exceptions;

namespace ChatApp.Domain.Entities;

/// <summary>
/// [DDD - Entity] Message thuộc về một Room.
/// Message không tự tồn tại độc lập → cần RoomId.
///
/// [OOP - Immutability] Sau khi Message được tạo, content không thể thay đổi
/// (chat message design: không edit được, chỉ có thể delete soft).
/// </summary>
public class Message : BaseEntity
{
    public string Content { get; private set; } = default!;
    public Guid SenderId { get; private set; }
    public Guid RoomId { get; private set; }
    public bool IsDeleted { get; private set; }

    // Navigation properties - EF Core sẽ populate
    public User Sender { get; private set; } = default!;
    public Room Room { get; private set; } = default!;

    protected Message() { }

    public static Message Create(string content, Guid senderId, Guid roomId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Message content cannot be empty.");

        if (content.Length > 2000)
            throw new DomainException("Message content cannot exceed 2000 characters.");

        var message = new Message
        {
            Content = content.Trim(),
            SenderId = senderId,
            RoomId = roomId,
            IsDeleted = false
        };

        // [Domain Event] Sau khi tạo message, raise event để:
        // 1. Kafka producer gửi lên topic "chat-messages"
        // 2. RabbitMQ fan-out notification đến các subscriber
        // 3. SignalR hub broadcast đến clients trong room
        message.AddDomainEvent(new MessageSentEvent(
            message.Id, message.Content, senderId, roomId));

        return message;
    }

    /// <summary>
    /// [Design Pattern - Soft Delete] Không xóa vật lý khỏi DB.
    /// Giúp audit trail, compliance, và có thể restore.
    /// </summary>
    public void SoftDelete()
    {
        if (IsDeleted)
            throw new BusinessRuleException("Message is already deleted.");

        IsDeleted = true;
        SetUpdatedAt();
    }
}
