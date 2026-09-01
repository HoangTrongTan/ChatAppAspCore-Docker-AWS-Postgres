using ChatApp.Domain.Entities;
using ChatApp.Domain.Interfaces.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChatApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// [Design Pattern - Repository Pattern]
/// Implement domain interface → Infrastructure detail hidden.
///
/// [EF Core vs Dapper Strategy]
/// ┌──────────────────┬──────────────────────────────────────────┐
/// │ EF Core          │ WRITE operations (Insert, Update, Delete)│
/// │ Dapper Raw SQL   │ READ operations (Query, Report, Paginate)│
/// └──────────────────┴──────────────────────────────────────────┘
/// Lý do: EF Core tốt cho graph manipulation, Dapper tốt cho raw reads.
/// CQRS: Command side dùng EF Core, Query side dùng Dapper.
/// </summary>
public class MessageRepository(
    AppDbContext dbContext,
    NpgsqlDataSource dataSource // Dapper cần raw connection
) : IMessageRepository
{
    // ─── EF Core: Write operations ────────────────────────────────────
    public async Task AddAsync(Message message, CancellationToken ct = default)
        => await dbContext.Messages.AddAsync(message, ct);

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Messages
            .Include(m => m.Sender)  // Eager load Sender để map AvatarIndex
            .Include(m => m.Room)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    // ─── Dapper: Raw SQL Read operations ──────────────────────────────
    /// <summary>
    /// [Raw SQL via Dapper] Phân trang message của room.
    /// Raw SQL hiệu năng tốt hơn EF Core LINQ cho query phức tạp.
    ///
    /// Lệnh SQL tương đương:
    ///   SELECT m.*, u.username, u.avatar_index
    ///   FROM messages m
    ///   INNER JOIN users u ON m.sender_id = u.id
    ///   WHERE m.room_id = @RoomId AND m.is_deleted = false
    ///   ORDER BY m.created_at DESC
    ///   LIMIT @Limit OFFSET @Offset
    ///
    /// [System Design - Cursor Pagination] Production nên dùng cursor-based pagination
    /// thay vì OFFSET để tránh performance degradation khi data lớn.
    /// </summary>
    public async Task<IEnumerable<Message>> GetByRoomIdAsync(
        Guid roomId, int limit, int offset, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                m.id         AS Id,
                m.content    AS Content,
                m.sender_id  AS SenderId,
                m.room_id    AS RoomId,
                m.is_deleted AS IsDeleted,
                m.created_at AS CreatedAt,
                m.updated_at AS UpdatedAt,
                -- Sender fields (Dapper multi-mapping)
                u.id          AS Id,
                u.username    AS Username,
                u.avatar_index AS AvatarIndex,
                u.is_online   AS IsOnline,
                u.last_seen_at AS LastSeenAt,
                u.created_at  AS CreatedAt,
                u.updated_at  AS UpdatedAt
            FROM messages m
            INNER JOIN users u ON m.sender_id = u.id
            WHERE m.room_id = @RoomId
              AND m.is_deleted = false
            ORDER BY m.created_at ASC
            LIMIT @Limit OFFSET @Offset
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        // [Dapper Multi-mapping] Map 2 tables vào 2 objects và link chúng
        var messages = await connection.QueryAsync<Message, User, Message>(
            sql,
            (message, sender) =>
            {
                // Reflection workaround: set private navigation property
                // In production: dùng custom mapper hoặc separate read model
                typeof(Message)
                    .GetProperty(nameof(Message.Sender))!
                    .SetValue(message, sender);
                return message;
            },
            new { RoomId = roomId, Limit = limit, Offset = offset },
            splitOn: "Id" // Dapper biết tách 2 objects tại cột "Id" thứ 2
        );

        return messages;
    }

    /// <summary>
    /// [Raw SQL - Aggregate] COUNT query - dùng Dapper vì EF Core .CountAsync()
    /// cũng generate SQL tốt nhưng Dapper explicit hơn.
    /// </summary>
    public async Task<int> GetCountByRoomIdAsync(Guid roomId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*) FROM messages
            WHERE room_id = @RoomId AND is_deleted = false
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(sql, new { RoomId = roomId });
    }
}
