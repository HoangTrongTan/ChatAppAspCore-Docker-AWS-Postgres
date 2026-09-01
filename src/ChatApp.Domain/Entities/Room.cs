using ChatApp.Domain.Common;
using ChatApp.Domain.Exceptions;

namespace ChatApp.Domain.Entities;

/// <summary>
/// [DDD - Aggregate Root] Room là aggregate root của chat group.
/// Room quản lý danh sách Members và có thể add/remove.
///
/// [OOP - Encapsulation] _members là private list,
/// expose ra ngoài qua IReadOnlyCollection để prevent external mutation.
/// </summary>
public class Room : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public bool IsPublic { get; private set; } = true;
    public Guid CreatedByUserId { get; private set; }

    // [OOP - Encapsulation] Backing field private, expose read-only
    private readonly List<RoomMember> _members = new();
    public IReadOnlyCollection<RoomMember> Members => _members.AsReadOnly();

    protected Room() { }

    public static Room Create(string name, Guid createdByUserId, string description = "", bool isPublic = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room name cannot be empty.");

        return new Room
        {
            Name = name.Trim(),
            Description = description,
            IsPublic = isPublic,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>
    /// [Business Logic tại Domain] Kiểm tra member trùng lặp
    /// và thêm vào danh sách nếu hợp lệ.
    /// </summary>
    public void AddMember(Guid userId)
    {
        if (_members.Any(m => m.UserId == userId))
            return; // Idempotent - không throw, chỉ bỏ qua

        _members.Add(RoomMember.Create(Id, userId));
        SetUpdatedAt();
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            throw new NotFoundException(nameof(RoomMember), userId);

        _members.Remove(member);
        SetUpdatedAt();
    }

    public bool HasMember(Guid userId)
        => _members.Any(m => m.UserId == userId);
}
