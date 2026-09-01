namespace ChatApp.Application.DTOs;

/// <summary>
/// [DTO - Data Transfer Object] Chỉ chứa data cần trả về client.
/// Sử dụng init-only properties + constructor rỗng để AutoMapper map mượt mà.
/// </summary>
public record MessageDto
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public int AvatarIndex { get; init; }
    public Guid RoomId { get; init; }
    public DateTime Timestamp { get; init; }
    public bool IsDeleted { get; init; }
}

public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public int AvatarIndex { get; init; }
    public bool IsOnline { get; init; }
    public DateTime? LastSeenAt { get; init; }
}

public record RoomDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public int MemberCount { get; init; }
}

/// <summary>
/// Request/Response DTOs cho API Controllers (Những cái này không qua AutoMapper nên giữ nguyên Positional Record ngon lành)
/// </summary>
public record SendMessageRequest(string Content, string RoomName);
public record JoinRoomRequest(string Username, int AvatarIndex, string RoomName);
public record CreateUserRequest(string Username, int AvatarIndex);
public record UpdateAvatarRequest(int AvatarIndex);

/// <summary>
/// [Wrapper Pattern] Chuẩn hoá response format cho tất cả API endpoints.
/// </summary>
public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message = null,
    IEnumerable<string>? Errors = null
)
{
    public static ApiResponse<T> Ok(T data, string? message = null)
        => new(true, data, message);

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null)
        => new(false, default, message, errors);
}