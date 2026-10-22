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

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<ProcessedNotificationEvent> ProcessedEvents => Set<ProcessedNotificationEvent>();

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
    }
}
