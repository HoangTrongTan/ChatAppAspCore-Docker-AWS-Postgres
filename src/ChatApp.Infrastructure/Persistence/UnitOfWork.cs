using ChatApp.Domain.Common;
using ChatApp.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Persistence;

/// <summary>
/// [Design Pattern - Unit of Work]
/// Bao bọc DbContext, quản lý transaction.
/// Sau khi SaveChanges → dispatch domain events qua MediatR.
///
/// [SOLID - Single Responsibility] UnitOfWork chỉ biết:
///   1. Gọi SaveChangesAsync
///   2. Thu thập domain events từ tracked entities
///   3. Publish chúng qua MediatR
/// Không biết gì về Redis, Kafka, RabbitMQ.
/// </summary>
public sealed class UnitOfWork(
    AppDbContext dbContext,
    IPublisher publisher // MediatR IPublisher để dispatch domain events
) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await dbContext.SaveChangesAsync(ct);

    /// <summary>
    /// [Domain Event Dispatch] Sau khi persist xong:
    ///   1. Thu thập tất cả domain events từ tracked entities
    ///   2. Clear events (tránh publish 2 lần)
    ///   3. Publish qua MediatR → handlers của từng event type sẽ xử lý
    ///
    /// Pattern: Transactional Outbox (đơn giản hóa)
    /// Production: dùng Outbox pattern đầy đủ với bảng OutboxMessages
    /// để đảm bảo at-least-once delivery khi app crash.
    /// </summary>
    public async Task SaveChangesAndDispatchEventsAsync(CancellationToken ct = default)
    {
        // Thu thập domain events TRƯỚC khi save
        var domainEvents = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        await dbContext.SaveChangesAsync(ct);

        // Clear events trên entities sau khi save thành công
        foreach (var entry in dbContext.ChangeTracker.Entries<BaseEntity>())
            entry.Entity.ClearDomainEvents();

        // 👇 CHỈ PUBLISH KHI LÀ INotification ĐỂ TRÁNH CRASH RUNTIME
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is MediatR.INotification notification)
            {
                await publisher.Publish(notification, ct);
            }
        }
    }

    public async ValueTask DisposeAsync()
        => await dbContext.DisposeAsync();
}
