using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatApp.Infrastructure.Messaging.Kafka;

/// <summary>
/// [Kafka Consumer - BackgroundService]
/// Chạy liên tục như 1 hosted service trong background.
/// Poll messages từ Kafka topic → process → commit offset.
///
/// [System Design - Consumer Group]
/// Nhiều instances của app cùng consumer group → Kafka tự phân phối partitions.
/// Scale horizontally: thêm instances → tự động load balance.
///
/// [At-Least-Once Delivery] Enable.AutoCommit = false → manually commit
/// sau khi xử lý thành công → đảm bảo không mất message khi crash.
/// Trade-off: có thể process duplicate → handlers cần idempotent.
/// </summary>
public class KafkaConsumerService(
    IConfiguration configuration,
    ILogger<KafkaConsumerService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
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

            // [Consumer Group] Tất cả instances cùng GroupId chia sẻ partitions
            GroupId = configuration["Kafka:GroupId"] ?? "chatapp-consumer-group",

            // [Offset Reset] Khi consumer group mới → bắt đầu từ earliest message
            AutoOffsetReset = AutoOffsetReset.Earliest,

            // [Manual Commit] Tự commit sau khi xử lý thành công
            EnableAutoCommit = false,

            // [Session Timeout] Kafka broker đánh dấu consumer dead nếu không heartbeat
            SessionTimeoutMs = 45000,
            HeartbeatIntervalMs = 15000,

            // [Max Poll] Tối đa messages mỗi poll
            MaxPollIntervalMs = 300000,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
                logger.LogError("[Kafka Consumer] Error: {Reason}", e.Reason))
            .Build();

        // Subscribe vào topics cần xử lý
        consumer.Subscribe(["chat-messages", "user-events", "room-events"]);
        logger.LogInformation("[Kafka Consumer] Started, listening to topics...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // [Polling] Poll với timeout → không block indefinitely
                var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(100));
                if (consumeResult is null) continue;

                await ProcessMessageAsync(consumeResult, stoppingToken);

                // [Manual Commit] Commit sau khi xử lý thành công
                consumer.Commit(consumeResult);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "[Kafka Consumer] Consume error: {Reason}", ex.Error.Reason);
            }
            catch (OperationCanceledException)
            {
                break; // Graceful shutdown
            }
            catch (Exception ex)
            {
                // [Dead Letter Queue Pattern]
                // Production: gửi message lỗi vào "chat-messages-dlq" topic để retry sau
                logger.LogError(ex, "[Kafka Consumer] Unexpected error processing message");
            }
        }

        consumer.Close(); // Graceful: trigger rebalance để trao partition cho consumer khác
        logger.LogInformation("[Kafka Consumer] Stopped.");
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> result, CancellationToken ct)
    {
        var eventType = result.Message.Headers
            .FirstOrDefault(h => h.Key == "event-type")
            ?.GetValueBytes()
            .Let(b => System.Text.Encoding.UTF8.GetString(b));

        logger.LogInformation(
            "[Kafka Consumer] Processing {EventType} from {Topic} partition {Partition}",
            eventType, result.Topic, result.Partition.Value);

        // [Dispatch to handlers based on event type]
        // Production: dùng IMediator để dispatch đến INotificationHandler
        // hoặc dùng polymorphic dispatch với factory
        await Task.Delay(10, ct); // Placeholder: thay bằng actual processing
    }
}

/// <summary>
/// C# extension method nhỏ để tránh viết nested method calls
/// </summary>
internal static class ExtensionMethods
{
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> func) => func(value);
}
