using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ChatApp.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// [RabbitMQ via MassTransit]
/// RabbitMQ phù hợp cho:
///   - Task queues (retry, dead letter)
///   - Request/Reply pattern
///   - Routing: topic exchange (route by event type)
///   - Fan-out: broadcast đến nhiều queues
///
/// [Kafka vs RabbitMQ - khi nào dùng gì?]
/// ┌─────────────┬──────────────────────┬──────────────────────┐
/// │             │ Kafka                │ RabbitMQ             │
/// ├─────────────┼──────────────────────┼──────────────────────┤
/// │ Use case    │ Event streaming      │ Task queuing         │
/// │ Retention   │ Lưu lâu dài          │ Xóa sau khi consume  │
/// │ Throughput  │ Rất cao (millions/s) │ High (tens K/s)      │
/// │ Consumer    │ Pull (consumer poll) │ Push (broker push)   │
/// │ Retry       │ Manual (DLQ)         │ Built-in             │
/// └─────────────┴──────────────────────┴──────────────────────┘
///
/// Trong ChatApp:
/// - Kafka: stream chat messages (high throughput, audit trail)
/// - RabbitMQ: push notifications, email alerts (reliable delivery, retry)
///
/// [Design Pattern - MassTransit wrapper]
/// MassTransit abstract hóa RabbitMQ API → dễ swap sang ServiceBus, ActiveMQ.
/// </summary>

// ─── Message Contracts (shared between Publisher and Consumer) ────────────────

/// <summary>
/// [MassTransit Convention] Message type dùng interface thay vì class
/// để serialization cross-platform (Java, Python consumers cũng hiểu).
/// </summary>
public record NewMessageNotification
{
    public required Guid MessageId { get; init; }
    public required string Content { get; init; }
    public required string SenderUsername { get; init; }
    public required Guid RoomId { get; init; }
    public required string RoomName { get; init; }
    public required DateTime SentAt { get; init; }
}

public record UserPresenceNotification
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required bool IsOnline { get; init; }
}

// ─── Publisher ────────────────────────────────────────────────────────────────

/// <summary>
/// [MassTransit IPublishEndpoint] Publish message lên RabbitMQ exchange.
/// Exchange sẽ route đến queues dựa trên binding keys.
/// </summary>
public class RabbitMqPublisher(
    IPublishEndpoint publishEndpoint,
    ILogger<RabbitMqPublisher> logger
)
{
    public async Task PublishNewMessageAsync(NewMessageNotification notification, CancellationToken ct)
    {
        logger.LogDebug("[RabbitMQ] Publishing NewMessageNotification for room {RoomId}", notification.RoomId);

        // [Fan-out] Tất cả consumers subscribe "NewMessageNotification" đều nhận
        await publishEndpoint.Publish(notification, ct);
    }

    public async Task PublishUserPresenceAsync(UserPresenceNotification notification, CancellationToken ct)
    {
        await publishEndpoint.Publish(notification, ct);
    }
}

// ─── Consumers ────────────────────────────────────────────────────────────────

/// <summary>
/// [MassTransit Consumer] Xử lý NewMessageNotification từ RabbitMQ.
/// MassTransit tự động retry khi consumer throw exception.
///
/// [Design Pattern - Consumer] Mỗi consumer có 1 responsibility:
/// Đây là PushNotificationConsumer → chỉ gửi push notification.
/// Có thể thêm EmailConsumer, AnalyticsConsumer implement cùng interface.
/// </summary>
public class NewMessageConsumer(ILogger<NewMessageConsumer> logger)
    : IConsumer<NewMessageNotification>
{
    public async Task Consume(ConsumeContext<NewMessageNotification> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "[RabbitMQ Consumer] New message from {Sender} in {Room}: {Preview}",
            msg.SenderUsername, msg.RoomName,
            msg.Content.Length > 50 ? msg.Content[..50] + "..." : msg.Content);

        // TODO: Gửi push notification đến mobile clients
        // TODO: Send email notification nếu user offline > 5 phút
        // TODO: Update badge count

        await Task.CompletedTask;
    }
}

/// <summary>
/// [MassTransit Fault Consumer] Xử lý messages lỗi → dead letter queue.
/// Khi NewMessageConsumer throw exception quá số lần retry →
/// MassTransit gửi Fault&lt;NewMessageNotification&gt; vào queue riêng.
/// </summary>
public class NewMessageFaultConsumer(ILogger<NewMessageFaultConsumer> logger)
    : IConsumer<Fault<NewMessageNotification>>
{
    public Task Consume(ConsumeContext<Fault<NewMessageNotification>> context)
    {
        logger.LogError(
            "[RabbitMQ DLQ] Failed to process message {MessageId} after {RetryCount} retries. Exceptions: {Exceptions}",
            context.Message.Message.MessageId,
            context.Message.FaultedMessageId,
            string.Join(", ", context.Message.Exceptions.Select(e => e.Message)));

        // Production: alert on-call team, store in DLQ table for manual review
        return Task.CompletedTask;
    }
}
