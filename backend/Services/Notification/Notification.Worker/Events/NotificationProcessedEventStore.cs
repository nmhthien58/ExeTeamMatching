using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;
using Notification.Worker.Push;

namespace Notification.Worker.Events;

public sealed class NotificationProcessedEventStore(NotificationDbContext db, FcmPushDispatcher? push = null) : IProcessedEventStore
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct) =>
        db.ProcessedEvents.AnyAsync(x => x.EventId == eventId && x.Consumer == "notification", ct);

    public async Task MarkProcessedAsync(EventEnvelope envelope, CancellationToken ct)
    {
        var notifications = db.ChangeTracker.Entries<UserNotification>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        db.ProcessedEvents.Add(new ProcessedNotificationEvent
        {
            EventId = envelope.EventId,
            Topic = NotificationEventHandler.TopicFor(envelope.EventType),
            EventType = envelope.EventType,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        if (push is not null) await push.SendAsync(notifications, ct);
    }
}
