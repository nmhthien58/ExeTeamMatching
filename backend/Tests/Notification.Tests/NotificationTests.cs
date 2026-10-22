using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;
using Notification.Worker.Events;
using Notification.Worker.Notifications;

namespace Notification.Tests;

public sealed class NotificationTests
{
    private static NotificationDbContext Database() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ClaimsPrincipal Student(Guid id) => new(new ClaimsIdentity(
    [
        new Claim(ClaimNames.StudentId, id.ToString()),
        new Claim(ClaimNames.Roles, Roles.Student),
    ], "test", ClaimNames.Name, ClaimNames.Roles));

    [Fact]
    public async Task Contact_event_and_invitation_create_one_notification_and_email_log()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var handler = new NotificationEventHandler(db);
        var processed = new NotificationProcessedEventStore(db);
        var imported = EventEnvelope.Create("StudentImported",
            new { studentId, email = "student@example.test", fullName = "Sinh viên" }, "academic", null);
        await handler.HandleAsync(imported, default);
        await processed.MarkProcessedAsync(imported, default);
        var invitation = EventEnvelope.Create("InvitationSent",
            new { inviteeStudentId = studentId, teamName = "Nhóm A", teamId = Guid.NewGuid() }, "team", null);
        await handler.HandleAsync(invitation, default);
        await processed.MarkProcessedAsync(invitation, default);

        Assert.True(await processed.IsProcessedAsync(invitation.EventId, default));
        Assert.Single(db.Notifications);
        Assert.Equal(studentId, db.Notifications.Single().RecipientId);
        Assert.Equal("student@example.test", db.EmailLogs.Single().ToEmail);
    }

    [Fact]
    public async Task Email_disabled_keeps_app_notification_without_smtp_log()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        db.Contacts.Add(new Contact
        {
            Id = studentId, PersonType = "STUDENT", Email = "student@example.test",
            FullName = "Sinh viên", EmailEnabled = false,
        });
        await db.SaveChangesAsync();
        var envelope = EventEnvelope.Create("InvitationSent", new { inviteeStudentId = studentId }, "team", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Single(db.Notifications);
        Assert.Empty(db.EmailLogs);
    }

    [Fact]
    public async Task Read_api_service_scopes_to_jwt_person_and_marks_read()
    {
        await using var db = Database();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Notifications.AddRange(
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = mine,
                Type = "InvitationSent", Title = "A", Content = "A", CreatedAt = DateTimeOffset.UtcNow },
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = other,
                Type = "InvitationSent", Title = "B", Content = "B", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var service = new NotificationService(db);
        var user = Student(mine);

        var page = await service.ListAsync(user, 1, 20, false, default);
        Assert.Single(page.Items);
        Assert.Equal("A", page.Items[0].Title);
        await service.ReadAllAsync(user, default);
        Assert.Equal(0, (await service.UnreadAsync(user, default)).Count);
        Assert.False(db.Notifications.Single(x => x.RecipientId == other).IsRead);
    }
}
