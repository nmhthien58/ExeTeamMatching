using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;

namespace Notification.Worker.Events;

public sealed class NotificationProcessedEventStore(NotificationDbContext db) : IProcessedEventStore
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct) =>
        db.ProcessedEvents.AnyAsync(x => x.EventId == eventId && x.Consumer == "notification", ct);

    public async Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken ct)
    {
        db.ProcessedEvents.Add(new ProcessedNotificationEvent
        {
            EventId = envelope.EventId,
            Topic = NotificationEventHandler.TopicFor(envelope.EventType),
            EventType = envelope.EventType,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
