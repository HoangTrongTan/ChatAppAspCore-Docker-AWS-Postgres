using ChatApp.Domain.Common;

namespace ChatApp.Domain.Entities;

/// <summary>
/// [DDD - Value Object-like Entity] RoomMember là join table entity.
/// Không có business logic riêng, chỉ represent quan hệ User-Room.
/// </summary>
public class RoomMember : BaseEntity
{
    public Guid RoomId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public User User { get; private set; } = default!;
    public Room Room { get; private set; } = default!;

    protected RoomMember() { }

    public static RoomMember Create(Guid roomId, Guid userId) => new()
    {
        RoomId = roomId,
        UserId = userId,
        JoinedAt = DateTime.UtcNow
    };
}
