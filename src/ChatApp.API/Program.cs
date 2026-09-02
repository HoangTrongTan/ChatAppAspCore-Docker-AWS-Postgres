using ChatApp.API.Hubs;
using ChatApp.API.Middleware;
using ChatApp.Application;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Infrastructure;
using ChatApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

// 👉 Chỉ dùng khi chạy F5 trực tiếp trên Visual Studio không qua Docker:
#if DEBUG
DotNetEnv.Env.Load("../env/.env.dev");
#endif
/// <summary>
/// [ASP.NET Core 8 - Top-level Program.cs]
/// Minimal hosting model → không cần Startup.cs.
///
/// [Serilog] Structured logging: log ra console (dev) và file (prod).
/// Log format dạng JSON → dễ parse bởi ELK Stack, Grafana Loki.
/// </summary>
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/chatapp-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// [Serilog] Replace default .NET logging
builder.Host.UseSerilog();

// ─── Services Registration ────────────────────────────────────────────────────

// [Application Layer] MediatR + FluentValidation + AutoMapper
builder.Services.AddApplication();

// [Infrastructure Layer] EF Core + Dapper + Redis + Kafka + RabbitMQ
builder.Services.AddInfrastructure(builder.Configuration);

// [SignalR] Real-time hub
// Production: thêm .AddStackExchangeRedis(connStr) để scale multiple instances
builder.Services.AddSignalR(options =>
{
    // options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB max message
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
})
.AddJsonProtocol(options =>
{
    // [JSON Serialization] FE gửi camelCase (roomName, content),
    // C# record có PascalCase properties (RoomName, Content).
    // PropertyNameCaseInsensitive = true → deserialize không phân biệt hoa thường.
    // CamelCaseNamingPolicy         → serialize trả về FE dạng camelCase.
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

// [Adapter Pattern] SignalRChatNotifier implements IChatNotifier
// Application layer gọi IChatNotifier, không biết đây là SignalR
builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();

// [CORS] Cho phép Angular FE kết nối (development)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // SignalR yêu cầu AllowCredentials
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ChatApp API", Version = "v1" });
});

// [Health Checks] Monitoring endpoint → dùng với Kubernetes liveness/readiness probe
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("DefaultConnection is missing!"))
    .AddRedis(builder.Configuration["Redis:ConnectionString"] 
        ?? throw new InvalidOperationException("Redis ConnectionString is missing!"));

// ─── Build App ────────────────────────────────────────────────────────────────
var app = builder.Build();

// [Middleware Pipeline Order] Quan trọng: thứ tự middleware ảnh hưởng behavior
// Exception → Logging → CORS → Auth → Routing → Endpoints

// [Global Exception Handler] Phải đầu tiên để catch mọi exception
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatApp API v1"));
}

app.UseRouting();
app.UseCors("AllowAngular");

// [SignalR Endpoint] Route FE kết nối vào: ws://localhost:3000/hubs/chat
app.MapHub<ChatHub>("/hubs/chat");

// [REST Endpoints]
app.MapControllers();

// [Health Check Endpoints]
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// ─── Auto-migrate Database ────────────────────────────────────────────────────
/// <summary>
/// [EF Core Migration] Tự động migrate khi startup.
/// Production: nên chạy migration riêng trong CI/CD pipeline, không auto-migrate.
/// </summary>
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("Applying database migrations...");
        //await dbContext.Database.MigrateAsync();
        await dbContext.Database.EnsureCreatedAsync();
        Log.Information("Database migration completed.");

        // [Seed Data] Tạo room mặc định "general" nếu chưa có
        await SeedDataAsync(dbContext);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to apply database migrations. Application will exit.");
        throw;
    }
}

await app.RunAsync();

/// <summary>
/// [Database Seeder] Tạo dữ liệu mặc định.
/// Production: move vào IHostedService hoặc EF Core HasData().
/// </summary>
static async Task SeedDataAsync(AppDbContext dbContext)
{
    try
    {
        // Kiểm tra xem đã có room "general" chưa
        var exists = await dbContext.Rooms.AnyAsync(r => r.Name == "general");
        if (!exists)
        {
            var systemUser = ChatApp.Domain.Entities.User.Create("System", 0);
            dbContext.Users.Add(systemUser);

            var generalRoom = ChatApp.Domain.Entities.Room.Create(
                "general", systemUser.Id, "General chat room for everyone");
            dbContext.Rooms.Add(generalRoom);

            await dbContext.SaveChangesAsync();
            Log.Information("Default room 'general' created.");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Seed data skipped or already initialized.");
    }
}
