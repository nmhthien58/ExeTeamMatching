using Microsoft.EntityFrameworkCore;

namespace Notification.Worker.Data;

public sealed class Contact
{
    public Guid Id { get; set; }
    public string PersonType { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool EmailEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UserNotification
{
    public Guid Id { get; set; }
    public string RecipientType { get; set; } = "";
    public Guid? RecipientId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? LinkUrl { get; set; }
    public Guid SourceEventId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ProcessedNotificationEvent
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = "notification";
    public string Topic { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class EmailLog
{
    public Guid Id { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid SourceEventId { get; set; }
    public string ToEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string TemplateCode { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class DeviceToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<ProcessedNotificationEvent> ProcessedEvents => Set<ProcessedNotificationEvent>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("notification");
        model.Entity<Contact>(e =>
        {
            e.ToTable("contacts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasColumnType("citext");
        });
        model.Entity<UserNotification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SourceEventId, x.RecipientType, x.RecipientId }).IsUnique()
                .HasDatabaseName("uq_notifications_event_recipient");
        });
        model.Entity<ProcessedNotificationEvent>(e =>
        {
            e.ToTable("processed_events");
            e.HasKey(x => new { x.EventId, x.Consumer });
        });
        model.Entity<EmailLog>(e =>
        {
            e.ToTable("email_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.ToEmail).HasColumnType("citext");
            e.HasIndex(x => new { x.SourceEventId, x.ToEmail }).IsUnique()
                .HasDatabaseName("uq_email_logs_event_to");
            e.HasOne<UserNotification>().WithMany().HasForeignKey(x => x.NotificationId)
                .HasConstraintName("fk_email_logs_notification").OnDelete(DeleteBehavior.SetNull);
        });
        model.Entity<DeviceToken>(e =>
        {
            e.ToTable("device_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique().HasDatabaseName("uq_device_tokens_token");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_device_tokens_user");
        });
    }
}
