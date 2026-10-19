using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Outbox;

/// <summary>
/// Dòng của bảng <c>&lt;schema&gt;.outbox_messages</c> (deploy/db). <see cref="Id"/> chính là <c>eventId</c> của envelope.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }

    /// <summary>JSON của payload (cột jsonb).</summary>
    public string Payload { get; set; } = "{}";

    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Dòng của bảng <c>&lt;schema&gt;.processed_events</c>.</summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

public static class OutboxModelBuilderExtensions
{
    /// <summary>Map <see cref="OutboxMessage"/> vào <c>{schema}.outbox_messages</c>. Gọi trong <c>OnModelCreating</c>.</summary>
    public static ModelBuilder AddOutboxMessages(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages", schema);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(100);
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(50);
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.LastError).HasColumnName("last_error");
        });
        return modelBuilder;
    }

    /// <summary>Map <see cref="ProcessedEvent"/> vào <c>{schema}.processed_events</c>.</summary>
    public static ModelBuilder AddProcessedEvents(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<ProcessedEvent>(e =>
        {
            e.ToTable("processed_events", schema);
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        });
        return modelBuilder;
    }
}

public static class OutboxDbContextExtensions
{
    /// <summary>
    /// Ghi sự kiện vào outbox cùng transaction với thay đổi nghiệp vụ: gọi trước <c>SaveChangesAsync</c>,
    /// dispatcher sẽ phát lên Kafka sau.
    /// <code>db.AddOutboxMessage(Topics.Team, "TeamCreated", "Team", team.Id, new { teamId = team.Id, … });</code>
    /// </summary>
    public static OutboxMessage AddOutboxMessage<TPayload>(this DbContext db, string topic, string eventType,
        string aggregateType, Guid aggregateId, TPayload payload)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions),
            CorrelationId = CorrelationContext.Id,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        db.Set<OutboxMessage>().Add(message);
        return message;
    }
}
