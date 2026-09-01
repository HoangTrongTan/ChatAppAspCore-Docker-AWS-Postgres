using ChatApp.Domain.Entities;

namespace ChatApp.Domain.Interfaces.Repositories;

/// <summary>
/// [SOLID - Interface Segregation + Dependency Inversion]
/// Domain định nghĩa interface, Infrastructure implement.
/// Application layer chỉ biết interface, không biết EF Core hay Dapper.
/// Điều này cho phép swap DB engine mà không ảnh hưởng business logic.
///
/// [Design Pattern - Repository] Trừu tượng hóa data access.
/// Repository trả về Domain Entities, không phải DbSet hay IQueryable.
/// </summary>
public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// [Raw SQL via Dapper] Lấy messages có phân trang, dùng Dapper cho hiệu năng cao.
    /// EF Core tốt cho write, Dapper tốt cho read-heavy queries.
    /// CQRS pattern: Command dùng EF Core, Query dùng Dapper.
    /// </summary>
    Task<IEnumerable<Message>> GetByRoomIdAsync(Guid roomId, int limit, int offset, CancellationToken ct = default);

    Task AddAsync(Message message, CancellationToken ct = default);

    Task<int> GetCountByRoomIdAsync(Guid roomId, CancellationToken ct = default);
}
