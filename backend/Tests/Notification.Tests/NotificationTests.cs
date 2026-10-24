using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
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

    private static ClaimsPrincipal Admin() => new(new ClaimsIdentity(
    [
        new Claim(ClaimNames.Roles, Roles.Admin),
    ], "test", ClaimNames.Name, ClaimNames.Roles));

    [Fact]
    public async Task Admin_sees_only_global_admin_notifications_and_cannot_set_email_preferences()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        db.Notifications.AddRange(
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "ADMIN", RecipientId = null,
                Type = "Announcement", Title = "Quản trị", Content = "Nội dung", CreatedAt = DateTimeOffset.UtcNow },
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = studentId,
                Type = "InvitationSent", Title = "Sinh viên", Content = "Nội dung", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var page = await service.ListAsync(Admin(), 1, 20, false, default);
        Assert.Equal("Quản trị", Assert.Single(page.Items).Title);
        Assert.Equal(1, (await service.UnreadAsync(Admin(), default)).Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.PreferencesAsync(Admin(), default));
    }

    [Fact]
    public async Task Read_is_idempotent_and_missing_notification_is_not_found()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var notification = new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT",
            RecipientId = studentId, Type = "InvitationSent", Title = "Lời mời", Content = "Nội dung",
            CreatedAt = DateTimeOffset.UtcNow };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        await service.ReadAsync(Student(studentId), notification.Id, default);
        var firstReadAt = notification.ReadAt;
        await service.ReadAsync(Student(studentId), notification.Id, default);
        Assert.Equal(firstReadAt, notification.ReadAt);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ReadAsync(Student(studentId), Guid.NewGuid(), default));
    }

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

    [Fact]
    public async Task Change_request_event_deduplicates_recipients_and_emails_known_contacts()
    {
        await using var db = Database();
        var member = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Contacts.Add(new Contact
        {
            Id = member, PersonType = "STUDENT", Email = "member@example.test",
            FullName = "Thành viên", EmailEnabled = true,
        });
        await db.SaveChangesAsync();
        var envelope = EventEnvelope.Create("ChangeRequestApproved",
            new { memberStudentIds = new[] { member, member, other } }, "team", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Single(db.EmailLogs);
        Assert.Equal("member@example.test", db.EmailLogs.Single().ToEmail);
    }

    [Fact]
    public async Task Profile_update_without_contact_fields_preserves_existing_email_and_name()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        db.Contacts.Add(new Contact
        {
            Id = studentId, PersonType = "STUDENT", Email = "student@example.test",
            FullName = "Tên cũ", EmailEnabled = true,
        });
        await db.SaveChangesAsync();
        var envelope = EventEnvelope.Create("StudentProfileUpdated", new { studentId }, "academic", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        var contact = await db.Contacts.SingleAsync();
        Assert.Equal("student@example.test", contact.Email);
        Assert.Equal("Tên cũ", contact.FullName);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task List_filters_unread_paginates_and_rejects_invalid_page_size()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Notifications.AddRange(
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = studentId,
                Type = "A", Title = "Old", Content = "Old", IsRead = true, CreatedAt = now.AddMinutes(-2) },
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = studentId,
                Type = "B", Title = "Middle", Content = "Middle", CreatedAt = now.AddMinutes(-1) },
            new UserNotification { Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = studentId,
                Type = "C", Title = "Newest", Content = "Newest", CreatedAt = now });
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        var first = await service.ListAsync(Student(studentId), 1, 1, true, default);
        var second = await service.ListAsync(Student(studentId), 2, 1, true, default);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal("Newest", Assert.Single(first.Items).Title);
        Assert.Equal("Middle", Assert.Single(second.Items).Title);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.ListAsync(Student(studentId), 1, 101, false, default));
    }

    [Fact]
    public async Task Read_single_notification_checks_ownership_and_email_preferences_persist()
    {
        await using var db = Database();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(), RecipientType = "STUDENT", RecipientId = mine,
            Type = "InvitationSent", Title = "Lời mời", Content = "Nội dung", CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Notifications.Add(notification);
        db.Contacts.Add(new Contact
        {
            Id = mine, PersonType = "STUDENT", Email = "mine@example.test",
            FullName = "Sinh viên", EmailEnabled = true,
        });
        await db.SaveChangesAsync();
        var service = new NotificationService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReadAsync(Student(other), notification.Id, default));
        Assert.False(notification.IsRead);
        await service.ReadAsync(Student(mine), notification.Id, default);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        await service.SetPreferencesAsync(Student(mine), new EmailPreferences(false), default);
        Assert.False((await service.PreferencesAsync(Student(mine), default)).EmailEnabled);
    }
}
