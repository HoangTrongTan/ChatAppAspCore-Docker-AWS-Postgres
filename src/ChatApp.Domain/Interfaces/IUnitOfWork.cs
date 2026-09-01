namespace ChatApp.Domain.Interfaces;

/// <summary>
/// [Design Pattern - Unit of Work]
/// Quản lý transaction: nhiều repository operations trong 1 transaction.
/// Đảm bảo atomicity: hoặc tất cả thành công, hoặc rollback hết.
///
/// Ví dụ: SendMessage = AddMessage + UpdateRoomLastActivity
/// → Nếu lỗi ở bước 2, bước 1 cũng bị rollback.
///
/// [SOLID - Interface Segregation] IUnitOfWork không expose DbContext
/// → Application layer không biết gì về EF Core.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Dispatch domain events sau khi SaveChanges thành công.
    /// Domain events sẽ được published qua MediatR.
    /// </summary>
    Task SaveChangesAndDispatchEventsAsync(CancellationToken ct = default);
}
