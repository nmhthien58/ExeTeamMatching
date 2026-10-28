using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;
using Notification.Worker.Events;

namespace Notification.Tests;

public sealed class WorkspaceEmailPolicyTests
{
    [Theory]
    [InlineData("TaskDueSoon", false)]
    [InlineData("DeliverableOverdue", false)]
    [InlineData("MeetingReminder", true)]
    public async Task Reminders_use_the_documented_channel(string eventType, bool expectsEmail)
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        db.Contacts.Add(new Contact { Id = student, PersonType = "STUDENT", Email = "student@example.test",
            FullName = "Sinh viên", EmailEnabled = true });
        await db.SaveChangesAsync();
        var envelope = EventEnvelope.Create(eventType, new
        {
            workspaceId = Guid.NewGuid(), assigneeStudentIds = new[] { student },
            memberStudentIds = new[] { student },
            invitees = new[] { new { personType = "STUDENT", personId = student } },
        }, "jobs", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);
        Assert.Equal(student, Assert.Single(db.Notifications).RecipientId);
        Assert.Equal(expectsEmail ? 1 : 0, await db.EmailLogs.CountAsync());
    }
}
