using ChatApp.Domain.Common;
using ChatApp.Domain.Events;
using ChatApp.Domain.Exceptions;

namespace ChatApp.Domain.Entities;

/// <summary>
/// [DDD - Aggregate Root] User là Aggregate Root.
/// Mọi thay đổi liên quan đến User phải đi qua User entity.
///
/// [OOP - Encapsulation] Setter private → chỉ thay đổi qua methods.
/// Không để ngoài set trực tiếp vào property.
///
/// [SOLID - Single Responsibility] User chỉ biết về business logic của chính nó.
/// Không biết DB, không biết API, không biết cache.
/// </summary>
public class User : BaseEntity
{
    // Private setters → enforce encapsulation
    public string Username { get; private set; } = default!;
    public int AvatarIndex { get; private set; }
    public bool IsOnline { get; private set; }
    public DateTime? LastSeenAt { get; private set; }

    // EF Core requires parameterless ctor (protected to prevent misuse)
    protected User() { }

    // [Factory Method Pattern] - tạo User qua static factory thay vì constructor
    // Lý do: constructor không throw exception tốt, factory có thể validate trước
    public static User Create(string username, int avatarIndex)
    {
        // [Business Rule Validation] validate tại domain layer
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username cannot be empty.");

        if (username.Length < 2 || username.Length > 30)
            throw new DomainException("Username must be between 2 and 30 characters.");

        if (avatarIndex is < 0 or > 5)
            throw new DomainException("Avatar index must be between 0 and 5.");

        var user = new User
        {
            Username = username.Trim(),
            AvatarIndex = avatarIndex,
            IsOnline = false
        };

        // [Domain Event] Raise event khi user được tạo
        // Handler sẽ gửi welcome notification, log, etc.
        user.AddDomainEvent(new UserCreatedEvent(user.Id, user.Username));

        return user;
    }

    public void GoOnline()
    {
        if (IsOnline) return; // Idempotent
        IsOnline = true;
        SetUpdatedAt();
        AddDomainEvent(new UserStatusChangedEvent(Id, Username, true));
    }

    public void GoOffline()
    {
        if (!IsOnline) return;
        IsOnline = false;
        LastSeenAt = DateTime.UtcNow;
        SetUpdatedAt();
        AddDomainEvent(new UserStatusChangedEvent(Id, Username, false));
    }

    public void UpdateAvatar(int avatarIndex)
    {
        if (avatarIndex is < 0 or > 5)
            throw new DomainException("Avatar index must be between 0 and 5.");

        AvatarIndex = avatarIndex;
        SetUpdatedAt();
    }
}
