namespace ChatApp.Domain.Common;

/// <summary>
/// [OOP - Inheritance] BaseDomainEvent implement sẵn các props chung,
/// các event cụ thể chỉ cần thêm payload riêng.
/// </summary>
public abstract record BaseDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
