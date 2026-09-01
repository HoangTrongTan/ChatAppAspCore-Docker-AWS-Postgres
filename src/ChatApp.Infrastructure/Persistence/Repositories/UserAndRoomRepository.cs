using ChatApp.Domain.Entities;
using ChatApp.Domain.Interfaces.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChatApp.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext dbContext, NpgsqlDataSource dataSource) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Users.FindAsync([id], ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await dbContext.Users.AddAsync(user, ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => await dbContext.Users.AnyAsync(u => u.Username == username, ct);

    /// <summary>
    /// [Raw SQL - Dapper] Lấy online users.
    /// Dapper phù hợp cho query đơn giản, nhanh hơn EF Core.
    /// Production: nên lấy từ Redis presence tracking thay vì DB.
    /// </summary>
    public async Task<IEnumerable<User>> GetOnlineUsersAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT id AS Id, username AS Username, avatar_index AS AvatarIndex,
                   is_online AS IsOnline, last_seen_at AS LastSeenAt,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE is_online = true
            ORDER BY username
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.QueryAsync<User>(sql);
    }
}

public class RoomRepository(AppDbContext dbContext) : IRoomRepository
{
    public async Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Rooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Room?> GetByNameAsync(string name, CancellationToken ct = default)
        => await dbContext.Rooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IEnumerable<Room>> GetPublicRoomsAsync(CancellationToken ct = default)
        => await dbContext.Rooms
            .Where(r => r.IsPublic)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAsync(Room room, CancellationToken ct = default)
        => await dbContext.Rooms.AddAsync(room, ct);
}
