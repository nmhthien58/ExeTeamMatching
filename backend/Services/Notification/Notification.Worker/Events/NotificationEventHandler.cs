using System.Text.Json;
using BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;

namespace Notification.Worker.Events;

/// <summary>Dựng danh bạ và thông báo trong ứng dụng từ các event contract hiện hành.</summary>
public sealed class NotificationEventHandler(NotificationDbContext db) : IIntegrationEventHandler
{
    private static readonly (string Topic, string[] Types)[] Groups =
    [
        (Topics.Student, ["StudentImported", "StudentProfileUpdated", "LecturerRegistered", "LecturerProfileUpdated", "MentorRegistered"]),
        (Topics.Registration, ["RoundOpened", "RoundClosed", "DeadlineApproaching"]),
        (Topics.Team, ["TeamFinalized", "TeamNeedsReview", "ProjectChanged"]),
        (Topics.Invitation, ["InvitationSent", "InvitationAccepted", "InvitationDeclined", "InvitationExpired", "JoinRequestSubmitted", "JoinRequestAccepted", "JoinRequestDeclined"]),
        (Topics.ChangeRequest, ["ChangeRequestSubmitted", "ChangeRequestApproved", "ChangeRequestRejected"]),
        (Topics.Assignment, ["TopicInterestOpened", "TopicInterestClosed", "LecturerPreferencesUpdated", "LecturerAssigned", "MentorAssigned", "MentorUnassigned", "AdvisorMatchingCompleted"]),
    ];

    public static IEnumerable<(string Topic, string EventType)> Subscriptions =>
        Groups.SelectMany(g => g.Types.Select(t => (g.Topic, t)));

    public static string TopicFor(string eventType) =>
        Groups.First(g => g.Types.Contains(eventType, StringComparer.Ordinal)).Topic;

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken ct)
    {
        var data = envelope.Payload;
        if (TopicFor(envelope.EventType) == Topics.Student)
        {
            await UpdateContactAsync(envelope.EventType, data, ct);
            return;
        }

        var recipients = Recipients(envelope.EventType, data)
            .Where(r => r.Id is not null || r.Type == "ADMIN").Distinct().ToList();
        var (title, content, link) = Template(envelope.EventType, data);
        foreach (var (type, id) in recipients)
        {
            // Idempotency còn được bảo vệ bởi UNIQUE(source_event_id, recipient_type, recipient_id).
            var notification = new UserNotification
            {
                Id = Guid.NewGuid(), RecipientType = type, RecipientId = id,
                Type = envelope.EventType, Title = title, Content = content,
                LinkUrl = link, SourceEventId = envelope.EventId, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Notifications.Add(notification);
            if (id is not { } personId || !ShouldEmail(envelope.EventType)) continue;
            var contact = await db.Contacts.FindAsync([personId], ct);
            if (contact is null || !contact.EmailEnabled ||
                db.EmailLogs.Local.Any(x => x.SourceEventId == envelope.EventId &&
                    string.Equals(x.ToEmail, contact.Email, StringComparison.OrdinalIgnoreCase))) continue;
            db.EmailLogs.Add(new EmailLog
            {
                Id = Guid.NewGuid(), NotificationId = notification.Id, SourceEventId = envelope.EventId,
                ToEmail = contact.Email, Subject = title, TemplateCode = envelope.EventType,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        // MarkProcessedAsync lưu notification và processed_events cùng một transaction.
    }

    private static bool ShouldEmail(string type) => type is
        "InvitationSent" or "JoinRequestAccepted" or "ChangeRequestApproved" or "ChangeRequestRejected" or
        "DeadlineApproaching" or "LecturerAssigned" or "MentorAssigned";

    private async Task UpdateContactAsync(string eventType, JsonElement data, CancellationToken ct)
    {
        var (type, id) = eventType switch
        {
            "StudentImported" or "StudentProfileUpdated" => ("STUDENT", Id(data, "studentId")),
            "LecturerRegistered" or "LecturerProfileUpdated" => ("LECTURER", Id(data, "lecturerId")),
            "MentorRegistered" => ("MENTOR", Id(data, "mentorId")),
            _ => ("", null),
        };
        if (id is null) return;
        var existing = await db.Contacts.FindAsync([id.Value], ct);
        var email = String(data, "email");
        var name = String(data, "fullName");
        // LecturerProfileUpdated chỉ chứa chuyên môn/sức chứa; không xóa dữ liệu danh bạ đang có.
        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name)) return;
            db.Contacts.Add(new Contact { Id = id.Value, PersonType = type, Email = email, FullName = name, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(email)) existing.Email = email;
            if (!string.IsNullOrWhiteSpace(name)) existing.FullName = name;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static IEnumerable<(string Type, Guid? Id)> Recipients(string eventType, JsonElement data)
    {
        static IEnumerable<(string, Guid?)> Students(JsonElement data, string field) =>
            Ids(data, field).Select(id => ("STUDENT", (Guid?)id));

        switch (eventType)
        {
            case "InvitationSent": yield return ("STUDENT", Id(data, "inviteeStudentId")); break;
            case "InvitationAccepted": case "InvitationDeclined": case "InvitationExpired":
                yield return ("STUDENT", Id(data, "leaderStudentId")); break;
            case "JoinRequestSubmitted": yield return ("STUDENT", Id(data, "leaderStudentId")); break;
            case "JoinRequestAccepted": case "JoinRequestDeclined":
                yield return ("STUDENT", Id(data, "studentId")); break;
            case "ChangeRequestSubmitted": case "TeamNeedsReview": case "AdvisorMatchingCompleted":
                yield return ("ADMIN", null); break;
            case "ChangeRequestApproved": case "ChangeRequestRejected":
                foreach (var r in Students(data, "memberStudentIds")) yield return r;
                break;
            case "TopicInterestOpened":
                if (String(data, "advisorKind") == "LECTURER")
                    foreach (var r in Students(data, "memberStudentIds")) yield return r;
                break;
            case "LecturerPreferencesUpdated":
                foreach (var id in Ids(data, "addedLecturerIds")) yield return ("LECTURER", id);
                break;
            case "LecturerAssigned":
                yield return ("LECTURER", Id(data, "lecturerId"));
                foreach (var r in Students(data, "memberStudentIds")) yield return r;
                break;
            case "MentorAssigned":
                yield return ("MENTOR", Id(data, "mentorId"));
                foreach (var r in Students(data, "memberStudentIds")) yield return r;
                break;
            case "MentorUnassigned": yield return ("MENTOR", Id(data, "mentorId")); break;
            case "TeamFinalized":
                foreach (var r in Students(data, "memberStudentIds")) yield return r;
                break;
            case "DeadlineApproaching":
                foreach (var r in Students(data, "studentIdsWithoutTeam")) yield return r;
                break;
        }
    }

    private static (string Title, string Content, string? Link) Template(string type, JsonElement data)
    {
        var teamId = Id(data, "teamId");
        var teamLink = teamId is null ? null : $"app://teams/{teamId}";
        return type switch
        {
            "InvitationSent" => ("Lời mời vào nhóm", $"{String(data, "teamName") ?? "Một nhóm"} đã mời bạn tham gia.", "app://invitations"),
            "InvitationAccepted" => ("Lời mời được chấp nhận", "Sinh viên đã nhận lời mời vào nhóm.", teamLink),
            "InvitationDeclined" => ("Lời mời bị từ chối", "Sinh viên đã từ chối lời mời vào nhóm.", teamLink),
            "InvitationExpired" => ("Lời mời hết hạn", "Lời mời vào nhóm đã hết hạn.", teamLink),
            "JoinRequestSubmitted" => ("Yêu cầu tham gia nhóm", "Có sinh viên muốn tham gia nhóm của bạn.", teamLink),
            "JoinRequestAccepted" => ("Đã được nhận vào nhóm", "Yêu cầu tham gia nhóm của bạn đã được chấp nhận.", teamLink),
            "JoinRequestDeclined" => ("Yêu cầu bị từ chối", "Yêu cầu tham gia nhóm của bạn đã bị từ chối.", teamLink),
            "ChangeRequestSubmitted" => ("Yêu cầu đổi nhóm chờ duyệt", "Có yêu cầu đổi nhóm mới cần Bộ môn xem xét.", "app://admin/change-requests"),
            "ChangeRequestApproved" => ("Đổi nhóm đã được duyệt", "Bộ môn đã duyệt yêu cầu đổi nhóm.", "app://change-requests"),
            "ChangeRequestRejected" => ("Đổi nhóm bị từ chối", "Bộ môn đã từ chối yêu cầu đổi nhóm.", "app://change-requests"),
            "LecturerPreferencesUpdated" => ("Nhóm đã chọn bạn", "Một nhóm đã thêm bạn vào nguyện vọng giảng viên hướng dẫn.", "app://lecturer/topics"),
            "LecturerAssigned" => ("Đã ghép giảng viên hướng dẫn", $"Giảng viên {String(data, "lecturerName") ?? "hướng dẫn"} đã được ghép với nhóm.", teamLink),
            "MentorAssigned" => ("Đã ghép mentor", $"Mentor {String(data, "mentorName") ?? "của nhóm"} đã được ghép với nhóm.", teamLink),
            "MentorUnassigned" => ("Không còn phụ trách nhóm", "Bạn không còn là mentor của nhóm này.", teamLink),
            "AdvisorMatchingCompleted" => ("Đã ghép cố vấn", $"Còn {String(data, "unmatchedCount") ?? "0"} nhóm chưa có cố vấn.", "app://admin/advisor-matching"),
            "TeamFinalized" => ("Nhóm đã được chốt", "Danh sách thành viên nhóm đã được chốt.", teamLink),
            "TeamNeedsReview" => ("Nhóm cần xem xét", "Có nhóm chưa đủ điều kiện sau khi đóng đợt.", "app://admin/teams"),
            "DeadlineApproaching" => ("Sắp hết hạn lập nhóm", "Đợt lập nhóm sắp đóng. Hãy hoàn tất nhóm của bạn.", "app://teams"),
            _ => (type, "Có cập nhật mới liên quan đến nhóm của bạn.", teamLink),
        };
    }

    private static Guid? Id(JsonElement data, string name) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private static IEnumerable<Guid> Ids(JsonElement data, string name) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => Guid.TryParse(x.GetString(), out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty)
            : [];

    private static string? String(JsonElement data, string name) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) ? value.ToString() : null;
}
