using ChatApp.Application.Common.Interfaces;
using ChatApp.Domain.Interfaces;
using ChatApp.Domain.Interfaces.Repositories;
using ChatApp.Infrastructure.Caching;
using ChatApp.Infrastructure.Messaging.Kafka;
using ChatApp.Infrastructure.Messaging.RabbitMQ;
using ChatApp.Infrastructure.Persistence;
using ChatApp.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChatApp.Infrastructure;

/// <summary>
/// [Design Pattern - Extension Method] Infrastructure DI registration.
/// Gom tất cả registrations vào đây → Program.cs sạch.
///
/// [SOLID - Open/Closed] Thêm service mới → chỉ sửa file này.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Database (EF Core + PostgreSQL) ──────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    // [Resilience] Tự động retry khi PostgreSQL transient error
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);

                    // [Performance] Command timeout
                    npgsqlOptions.CommandTimeout(60);
                })
            // [Development] Log SQL queries
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .UseSnakeCaseNamingConvention() // 👈 Tự map Id -> id, RoomId -> room_id
            // 👇 BỎ QUA CẢNH BÁO PENDING MODEL CHANGES Ở ĐÂY NÈ NÍ:
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
        );

        // [Dapper] NpgsqlDataSource cho raw SQL connections
        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")!;
            return NpgsqlDataSource.Create(connectionString);
        });

        // ─── Repositories ─────────────────────────────────────────────────
        // [Scoped] Repository sống cùng lifetime với HTTP request
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ─── Redis ────────────────────────────────────────────────────────
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(connectionString);
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // ─── Kafka ────────────────────────────────────────────────────────
        // [Singleton] Producer tái sử dụng connection
        services.AddSingleton<IEventBus, KafkaProducer>();

        // [Hosted Service] Consumer chạy liên tục
        services.AddHostedService<KafkaConsumerService>();

        // ─── RabbitMQ (MassTransit) ────────────────────────────────────────
        services.AddMassTransit(bus =>
        {
            // [Consumers] Đăng ký tất cả consumers
            bus.AddConsumer<NewMessageConsumer>();
            bus.AddConsumer<NewMessageFaultConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

                if (host.Contains("cloudamqp.com"))
                {
                    // Chạy chuẩn TLS CloudAMQP cổng 5671
                    cfg.Host(new Uri($"amqps://{host}:5671/{virtualHost}"), h =>
                    {
                        h.Username(username);
                        h.Password(password);
                    });
                }
                else
                {
                    cfg.Host(host, virtualHost, h =>
                    {
                        h.Username(username);
                        h.Password(password);
                    });
                }

                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(5)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        // [Publisher] Inject để sử dụng trong Application layer nếu cần
        services.AddScoped<RabbitMqPublisher>();

        return services;
    }
}
