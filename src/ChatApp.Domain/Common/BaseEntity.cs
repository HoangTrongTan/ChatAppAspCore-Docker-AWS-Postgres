namespace ChatApp.Domain.Common;

/// <summary>
/// [SOLID - Single Responsibility] BaseEntity chỉ chịu trách nhiệm
/// quản lý định danh và domain events của một aggregate root.
///
/// [OOP - Abstraction] Class abstract → không thể instantiate trực tiếp,
/// chỉ được kế thừa bởi các Entity cụ thể.
///
/// [System Design - DDD] Mọi entity đều có Id riêng,
/// tránh dùng DB-generated Id ở tầng Domain.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    // [Design Pattern - Observer via Domain Events]
    // Domain Events giúp loosely coupling giữa các aggregate.
    // Thay vì gọi trực tiếp service khác, entity chỉ raise event,
    // và handlers sẽ xử lý bất đồng bộ (giống Pub/Sub).
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents()
        => _domainEvents.Clear();

    protected void SetUpdatedAt()
        => UpdatedAt = DateTime.UtcNow;
}
