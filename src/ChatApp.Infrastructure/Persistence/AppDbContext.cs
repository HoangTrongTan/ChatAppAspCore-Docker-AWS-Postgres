using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Persistence;

/// <summary>
/// [EF Core - DbContext] Là entry point cho tất cả DB operations.
/// Partial class cho phép mở rộng mà không sửa file gốc.
///
/// [Code First] Dùng Fluent API configuration (không dùng DataAnnotation)
/// → clean entity, không coupling với EF Core.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // [EF Core] Tự scan và apply tất cả IEntityTypeConfiguration trong assembly
        // Không cần gọi từng configuration một → maintainable
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // [Convention] Tất cả string mặc định varchar(255) thay vì nvarchar(max)
        configurationBuilder.Properties<string>()
            .HaveMaxLength(255);

        // DateTime luôn store UTC
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<DateTimeUtcConverter>();
    }
}

/// <summary>
/// [EF Core Value Converter] Đảm bảo DateTime luôn được xử lý là UTC.
/// Tránh bug timezone khi deploy lên server khác múi giờ.
/// </summary>
public class DateTimeUtcConverter()
    : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
