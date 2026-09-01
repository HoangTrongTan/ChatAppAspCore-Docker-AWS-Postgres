using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// [EF Core Fluent API] Tách configuration ra file riêng → Clean Code.
/// Không để attributes trong Entity → Domain không coupling với EF Core.
///
/// [Code First] Class này define schema → EF Migration sẽ tạo SQL tương ứng.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users"); // Snake_case convention cho PostgreSQL

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.AvatarIndex)
            .HasColumnName("avatar_index")
            .HasDefaultValue(0);

        builder.Property(u => u.IsOnline)
            .HasColumnName("is_online")
            .HasDefaultValue(false);

        builder.Property(u => u.LastSeenAt)
            .HasColumnName("last_seen_at");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");

        // [Index] Unique constraint trên username → fast lookup + prevent duplicate
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("ix_users_username");

        // [Index] Lọc online users nhanh
        builder.HasIndex(u => u.IsOnline)
            .HasDatabaseName("ix_users_is_online");
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
        .HasColumnName("id")
        .ValueGeneratedNever();

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(m => m.SenderId).HasColumnName("sender_id").IsRequired();
        builder.Property(m => m.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        // [Relationships] FK với User (Sender)
        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict); // Không cascade delete

        // FK với Room
        builder.HasOne(m => m.Room)
            .WithMany()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // [Index] Composite index: lấy messages theo room + time (phân trang)
        builder.HasIndex(m => new { m.RoomId, m.CreatedAt })
            .HasDatabaseName("ix_messages_room_id_created_at");

        // [Global Filter] Mặc định không trả về soft-deleted messages
        // Trừ khi dùng .IgnoreQueryFilters() explicitly
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(r => r.IsPublic).HasColumnName("is_public").HasDefaultValue(true);
        builder.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.Name).IsUnique()
            .HasDatabaseName("ix_rooms_name");

        // [Navigation - Owned collection]
        builder.HasMany(r => r.Members)
            .WithOne(rm => rm.Room)
            .HasForeignKey(rm => rm.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoomMemberConfiguration : IEntityTypeConfiguration<RoomMember>
{
    public void Configure(EntityTypeBuilder<RoomMember> builder)
    {
        builder.ToTable("room_members");

        builder.HasKey(rm => rm.Id);

        builder.Property(rm => rm.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(rm => rm.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(rm => rm.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(rm => rm.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");

        // [Unique Composite] Một user chỉ join room 1 lần
        builder.HasIndex(rm => new { rm.RoomId, rm.UserId })
            .IsUnique()
            .HasDatabaseName("ix_room_members_room_user");

        builder.HasOne(rm => rm.User)
            .WithMany()
            .HasForeignKey(rm => rm.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
