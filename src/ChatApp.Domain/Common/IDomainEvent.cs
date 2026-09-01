namespace ChatApp.Domain.Common;

/// <summary>
/// [Design Pattern - Marker Interface]
/// IDomainEvent đánh dấu một sự kiện trong domain.
/// Không có method → chỉ để type-safe dispatch qua MediatR INotification.
///
/// [SOLID - Interface Segregation]
/// Interface nhỏ, đặc biệt → không ép implement phương thức không cần thiết.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string EventType { get; }
}
