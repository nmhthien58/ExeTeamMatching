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
    public async Task Change_request_result_also_notifies_assigned_lecturer()
    {
        await using var db = Database();
        var lecturerId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("ChangeRequestRejected",
            new { lecturerId, memberStudentIds = Array.Empty<Guid>() }, "team", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal("LECTURER", notification.RecipientType);
        Assert.Equal(lecturerId, notification.RecipientId);
    }

    [Fact]
    public async Task Other_advisor_match_notifies_interested_mentor_only()
    {
        await using var db = Database();
        var mentorId = Guid.NewGuid();
        var handler = new NotificationEventHandler(db);
        var closed = EventEnvelope.Create("TopicInterestClosed",
            new { advisorKind = "MENTOR", advisorId = mentorId, reason = "OTHER_ADVISOR_MATCHED" }, "team", null);
        var withdrawn = EventEnvelope.Create("TopicInterestClosed",
            new { advisorKind = "MENTOR", advisorId = mentorId, reason = "WITHDRAWN" }, "team", null);
        await handler.HandleAsync(closed, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(closed, default);
        await handler.HandleAsync(withdrawn, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(withdrawn, default);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal("MENTOR", notification.RecipientType);
        Assert.Equal(mentorId, notification.RecipientId);
        Assert.Equal("TopicInterestClosed", notification.Type);
    }

    [Fact]
    public async Task Assignment_notification_includes_match_reason()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("LecturerAssigned", new
        {
            lecturerId = Guid.NewGuid(), lecturerName = "Cô An", memberStudentIds = new[] { studentId },
            reason = "MUTUAL", preferenceRank = 1,
        }, "team", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Contains("Hai bên cùng chọn", db.Notifications.Single(x => x.RecipientId == studentId).Content);
    }

    [Fact]
    public async Task Dual_role_email_preference_updates_both_contacts()
    {
        await using var db = Database();
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        db.Contacts.AddRange(
            new Contact { Id = lecturerId, PersonType = "LECTURER", Email = "advisor@example.test", FullName = "Cố vấn", EmailEnabled = true },
            new Contact { Id = mentorId, PersonType = "MENTOR", Email = "advisor@example.test", FullName = "Cố vấn", EmailEnabled = true });
        await db.SaveChangesAsync();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimNames.LecturerId, lecturerId.ToString()),
            new Claim(ClaimNames.MentorId, mentorId.ToString()),
        ], "test"));
        var service = new NotificationService(db);

        await service.SetPreferencesAsync(user, new EmailPreferences(false), default);

        Assert.False((await service.PreferencesAsync(user, default)).EmailEnabled);
        Assert.All(db.Contacts, contact => Assert.False(contact.EmailEnabled));
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

    [Fact]
    public async Task Checkpoint_event_notifies_members_but_plain_messages_are_not_subscribed()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("CheckpointReviewed",
            new { workspaceId = Guid.NewGuid(), checkpointNo = 2, memberStudentIds = new[] { studentId } },
            "collab", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(studentId, db.Notifications.Single().RecipientId);
        Assert.Contains("Checkpoint 2", db.Notifications.Single().Content);
        Assert.DoesNotContain(NotificationEventHandler.Subscriptions, x => x.EventType == "MessageSent");
    }

    [Fact]
    public async Task First_mentor_registration_creates_welcome_email_once()
    {
        await using var db = Database();
        var mentorId = Guid.NewGuid();
        var handler = new NotificationEventHandler(db);
        var processed = new NotificationProcessedEventStore(db);
        var first = EventEnvelope.Create("MentorRegistered",
            new { mentorId, fullName = "Mentor", email = "mentor@example.test" }, "academic", null);
        await handler.HandleAsync(first, default);
        await processed.MarkProcessedAsync(first, default);
        var updated = EventEnvelope.Create("MentorRegistered",
            new { mentorId, fullName = "Mentor", email = "mentor@example.test" }, "academic", null);
        await handler.HandleAsync(updated, default);
        await processed.MarkProcessedAsync(updated, default);

        Assert.Single(db.EmailLogs);
        Assert.Single(db.Notifications);
        Assert.Equal("MentorRegistered", db.EmailLogs.Single().TemplateCode);
    }

    [Fact]
    public async Task Mention_notifies_only_valid_people_without_email()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("UserMentioned", new
        {
            workspaceId,
            mentioned = new object[]
            {
                new { personType = "STUDENT", personId = studentId },
                new { personType = "MENTOR", personId = mentorId },
                new { personType = "STUDENT", personId = studentId },
                new { personType = "ADMIN", personId = Guid.NewGuid() },
            },
        }, "collab", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Contains(db.Notifications, x => x.RecipientType == "STUDENT" && x.RecipientId == studentId);
        Assert.Contains(db.Notifications, x => x.RecipientType == "MENTOR" && x.RecipientId == mentorId);
        Assert.All(db.Notifications, x => Assert.Equal($"app://workspaces/{workspaceId}", x.LinkUrl));
        Assert.Empty(db.EmailLogs);
    }

    [Fact]
    public async Task Due_soon_reminder_emails_assignees_but_not_unrelated_contacts()
    {
        await using var db = Database();
        var assignee = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        db.Contacts.AddRange(
            new Contact { Id = assignee, PersonType = "STUDENT", Email = "assignee@example.test",
                FullName = "Người nhận", EmailEnabled = true },
            new Contact { Id = unrelated, PersonType = "STUDENT", Email = "other@example.test",
                FullName = "Người khác", EmailEnabled = true });
        await db.SaveChangesAsync();
        var envelope = EventEnvelope.Create("TaskDueSoon", new
        {
            workspaceId = Guid.NewGuid(), title = "Nộp báo cáo", assigneeStudentIds = new[] { assignee },
        }, "jobs", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(assignee, Assert.Single(db.Notifications).RecipientId);
        Assert.Equal("assignee@example.test", Assert.Single(db.EmailLogs).ToEmail);
        Assert.Equal("TaskDueSoon", db.EmailLogs.Single().TemplateCode);
    }

    [Fact]
    public async Task Meeting_reminder_notifies_invited_student_and_lecturer()
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("MeetingReminder", new
        {
            title = "Họp tuần", invitees = new object[]
            {
                new { personType = "STUDENT", personId = studentId },
                new { personType = "LECTURER", personId = lecturerId },
            },
        }, "jobs", null);
        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Contains(db.Notifications, x => x.RecipientType == "STUDENT" && x.RecipientId == studentId);
        Assert.Contains(db.Notifications, x => x.RecipientType == "LECTURER" && x.RecipientId == lecturerId);
    }

    [Theory]
    [InlineData("LecturerAssigned", "MUTUAL", "hai phía cùng chọn")]
    [InlineData("LecturerAssigned", "PREFERENCE", "nguyện vọng của nhóm")]
    [InlineData("LecturerAssigned", "LECTURER_INTEREST", "giảng viên quan tâm")]
    [InlineData("LecturerAssigned", "PREVIOUS_INSTRUCTOR", "giảng viên hướng dẫn kỳ trước")]
    [InlineData("LecturerAssigned", "DOMAIN", "phù hợp chuyên môn")]
    [InlineData("LecturerAssigned", "MANUAL", "Bộ môn phân công")]
    [InlineData("MentorAssigned", "MENTOR_INTEREST", "mentor quan tâm")]
    [InlineData("MentorAssigned", "DOMAIN", "phù hợp chuyên môn")]
    [InlineData("MentorAssigned", "MANUAL", "Bộ môn phân công")]
    public async Task Advisor_assignment_explains_reason_to_advisor_and_students(
        string eventType, string reason, string explanation)
    {
        await using var db = Database();
        var advisorId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var payload = eventType == "LecturerAssigned"
            ? (object)new { lecturerId = advisorId, lecturerName = "Giảng viên A",
                reason, memberStudentIds = new[] { studentId } }
            : new { mentorId = advisorId, mentorName = "Mentor A",
                reason, memberStudentIds = new[] { studentId } };
        var envelope = EventEnvelope.Create(eventType, payload, "assignment", null);

        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.All(db.Notifications, item => Assert.Contains(explanation, item.Content));
        Assert.Contains(db.Notifications, item => item.RecipientId == studentId);
        Assert.Contains(db.Notifications, item => item.RecipientId == advisorId);
    }

    [Theory]
    [InlineData("LEADER", "Đã đặt giờ họp hằng tuần")]
    [InlineData("INSTRUCTOR", "Giảng viên đã đổi giờ họp hằng tuần")]
    [InlineData("AUTO", "Hệ thống đã xếp giờ họp hằng tuần")]
    public async Task Meeting_slot_booking_explains_source_to_students(string source, string expectedTitle)
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var envelope = EventEnvelope.Create("MeetingSlotBooked", new
        {
            workspaceId = Guid.NewGuid(), startTime = "09:15", source,
            recipients = new[] { new { personType = "STUDENT", personId = studentId } },
        }, "collab", null);

        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(expectedTitle, notification.Title);
        Assert.Contains("09:15", notification.Content);
        Assert.Equal(studentId, notification.RecipientId);
    }

    [Fact]
    public async Task Task_and_deliverable_events_notify_only_intended_people_and_email_overdue()
    {
        await using var db = Database();
        var firstStudent = Guid.NewGuid();
        var secondStudent = Guid.NewGuid();
        var lecturer = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        foreach (var (id, type) in new[]
                 {
                     (firstStudent, "STUDENT"), (secondStudent, "STUDENT"),
                     (lecturer, "LECTURER"), (unrelated, "STUDENT"),
                 })
        {
            db.Contacts.Add(new Contact { Id = id, PersonType = type,
                Email = $"{id}@example.test", FullName = "Người nhận", EmailEnabled = true });
        }
        await db.SaveChangesAsync();
        var workspaceId = Guid.NewGuid();
        var cases = new (string Type, object Payload, Guid[] Recipients, int Emails)[]
        {
            ("TaskAssigned", new { workspaceId, title = "Việc mới",
                assigneeStudentIds = new[] { firstStudent } }, [firstStudent], 0),
            ("DeliverableCreated", new { workspaceId, title = "Bài nộp mới",
                memberStudentIds = new[] { firstStudent, secondStudent } },
                [firstStudent, secondStudent], 0),
            ("DeliverableSubmitted", new { workspaceId, lecturerId = lecturer }, [lecturer], 0),
            ("DeliverableReviewed", new { workspaceId, reviewStatus = "ACCEPTED",
                memberStudentIds = new[] { firstStudent, secondStudent } },
                [firstStudent, secondStudent], 0),
            ("DeliverableOverdue", new { workspaceId, title = "Bài nộp trễ",
                memberStudentIds = new[] { firstStudent, secondStudent }, lecturerId = lecturer },
                [firstStudent, secondStudent, lecturer], 3),
        };
        var handler = new NotificationEventHandler(db);
        var processed = new NotificationProcessedEventStore(db);

        foreach (var (type, payload, expected, emailCount) in cases)
        {
            var envelope = EventEnvelope.Create(type, payload, "collab", null);
            await handler.HandleAsync(envelope, default);
            await processed.MarkProcessedAsync(envelope, default);
            var notices = db.Notifications.Where(x => x.SourceEventId == envelope.EventId).ToList();
            Assert.Equal(expected.Length, notices.Count);
            foreach (var id in expected) Assert.Contains(notices, x => x.RecipientId == id);
            Assert.DoesNotContain(notices, x => x.RecipientId == unrelated);
            Assert.Equal(emailCount, db.EmailLogs.Count(x => x.SourceEventId == envelope.EventId));
            Assert.All(notices, x => Assert.Equal($"app://workspaces/{workspaceId}", x.LinkUrl));
        }
    }

    [Theory]
    [InlineData("MeetingProposed")]
    [InlineData("MeetingScheduled")]
    [InlineData("MeetingCancelled")]
    public async Task Meeting_changes_notify_invitees_without_email(string eventType)
    {
        await using var db = Database();
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(eventType, new
        {
            workspaceId,
            invitees = new object[]
            {
                new { personType = "STUDENT", personId = studentId },
                new { personType = "MENTOR", personId = mentorId },
            },
        }, "collab", null);

        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Contains(db.Notifications, x => x.RecipientType == "STUDENT" && x.RecipientId == studentId);
        Assert.Contains(db.Notifications, x => x.RecipientType == "MENTOR" && x.RecipientId == mentorId);
        Assert.Empty(db.EmailLogs);
    }

    [Theory]
    [InlineData("AdvisorQuestionAsked", "LECTURER")]
    [InlineData("AdvisorQuestionAnswered", "STUDENT")]
    [InlineData("MeetingSlotsPublished", "STUDENT")]
    public async Task Advisor_questions_and_published_slots_notify_explicit_recipients(
        string eventType, string recipientType)
    {
        await using var db = Database();
        var recipientId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(eventType, new
        {
            workspaceId, preview = "Cần góp ý đề tài",
            recipients = new[] { new { personType = recipientType, personId = recipientId } },
        }, "collab", null);

        await new NotificationEventHandler(db).HandleAsync(envelope, default);
        await new NotificationProcessedEventStore(db).MarkProcessedAsync(envelope, default);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(recipientType, notification.RecipientType);
        Assert.Equal(recipientId, notification.RecipientId);
        if (eventType == "MeetingSlotsPublished")
            Assert.Equal("app://meeting-schedule", notification.LinkUrl);
        else
            Assert.Equal($"app://workspaces/{workspaceId}", notification.LinkUrl);
        Assert.Empty(db.EmailLogs);
    }
}
