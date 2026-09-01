using ChatApp.Domain.Entities;

namespace ChatApp.Domain.Interfaces.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IEnumerable<Room>> GetPublicRoomsAsync(CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
}
