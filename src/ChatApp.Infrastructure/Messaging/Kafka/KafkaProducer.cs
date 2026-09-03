using System.Text.Json;
using ChatApp.Application.Common.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChatApp.Infrastructure.Messaging.Kafka;

/// <summary>
/// [Apache Kafka] Message Streaming Platform.
/// Kafka phù hợp cho:
///   - High-throughput event streaming (hàng triệu messages/giây)
///   - Audit log (message lưu vĩnh viễn theo retention policy)
///   - Replay events (có thể đọc lại events cũ)
///   - Fan-out đến nhiều consumers (multiple consumer groups)
///
/// [System Design - Event-Driven Architecture]
/// ChatApp.API → Kafka Topic "chat-messages" → Multiple Consumers:
///   - Chat Consumer: lưu vào DB
///   - Analytics Consumer: tổng hợp thống kê
///   - Notification Consumer: push notification mobile
///   - Search Consumer: index vào Elasticsearch
///
/// [Design Pattern - Producer/Consumer]
/// KafkaProducer chỉ publish, không biết ai consume → loosely coupled.
/// </summary>
public class KafkaProducer(
    IConfiguration configuration,
    ILogger<KafkaProducer> logger
) : IEventBus, IAsyncDisposable
{
    // [Lazy initialization] Producer được tạo 1 lần, reused cho nhiều messages
    private readonly Lazy<IProducer<string, string>> _producer = new(() =>
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",

            SecurityProtocol = Enum.TryParse<SecurityProtocol>(configuration["Kafka:SecurityProtocol"], true, out var sec)
                ? sec
                : SecurityProtocol.Plaintext,

            SaslMechanism = Enum.TryParse<SaslMechanism>(configuration["Kafka:SaslMechanism"], true, out var mech)
                ? mech
                : null,

            SaslUsername = configuration["Kafka:SaslUsername"],
            SaslPassword = configuration["Kafka:SaslPassword"],

            // [Reliability Config]
            // Acks.All: chờ tất cả replicas confirm → đảm bảo durability
            Acks = Acks.All,

            // Retry khi gặp lỗi transient
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,

            // [Idempotent Producer] Prevent duplicate messages khi retry
            EnableIdempotence = true,

            // [Compression] Giảm network bandwidth
            CompressionType = CompressionType.Snappy,

            // [Batching] Gom nhiều messages vào 1 batch → throughput cao hơn
            LingerMs = 5,         // Đợi 5ms để gom batch
            BatchSize = 65536,    // 64KB per batch
        };

        return new ProducerBuilder<string, string>(config).Build();
    });

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task PublishAsync<T>(T @event, string? topic = null, CancellationToken ct = default)
        where T : class
    {
        var topicName = topic ?? GetTopicName<T>();
        var key = Guid.NewGuid().ToString(); // Message key = partition key
        var value = JsonSerializer.Serialize(@event, JsonOptions);

        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            // [Kafka Headers] Metadata không phải business data
            Headers = new Headers
            {
                { "event-type", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")) }
            }
        };

        try
        {
            var result = await _producer.Value.ProduceAsync(topicName, message, ct);

            logger.LogInformation(
                "[Kafka] Published {EventType} to topic {Topic} partition {Partition} offset {Offset}",
                typeof(T).Name, topicName, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex,
                "[Kafka] Failed to publish {EventType} to topic {Topic}: {Error}",
                typeof(T).Name, topicName, ex.Error.Reason);

            // [Resilience] Kafka fail không nên block main chat flow
            // Production: implement Dead Letter Queue hoặc retry với exponential backoff
            throw;
        }
    }

    /// <summary>
    /// [Convention] Event type name → Kafka topic name mapping.
    /// MessageSentEvent → "chat-messages"
    /// UserJoinedRoomEvent → "user-events"
    /// </summary>
    private static string GetTopicName<T>() => typeof(T).Name switch
    {
        var n when n.Contains("Message") => "chat-messages",
        var n when n.Contains("User")    => "user-events",
        var n when n.Contains("Room")    => "room-events",
        _                                 => "general-events"
    };

    public async ValueTask DisposeAsync()
    {
        if (_producer.IsValueCreated)
        {
            _producer.Value.Flush(TimeSpan.FromSeconds(5));
            _producer.Value.Dispose();
        }
        await ValueTask.CompletedTask;
    }
}
