using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Notification.Worker.Data;

namespace Notification.Worker.Notifications;

public sealed record NotificationItem(Guid Id, string Type, string Title, string Content,
    string? LinkUrl, bool IsRead, DateTimeOffset CreatedAt);
public sealed record NotificationPage(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyList<NotificationItem> Items);
public sealed record UnreadCount(int Count);
public sealed record EmailPreferences(bool EmailEnabled);

public sealed class NotificationService(NotificationDbContext db)
{
    public async Task<NotificationPage> ListAsync(ClaimsPrincipal user, int page, int pageSize,
        bool unreadOnly, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = Mine(user);
        if (unreadOnly) query = query.Where(x => !x.IsRead);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NotificationItem(x.Id, x.Type, x.Title, x.Content, x.LinkUrl, x.IsRead, x.CreatedAt))
            .ToListAsync(ct);
        return new NotificationPage(page, pageSize, count, (count + pageSize - 1) / pageSize, items);
    }

    public async Task<UnreadCount> UnreadAsync(ClaimsPrincipal user, CancellationToken ct) =>
        new(await Mine(user).CountAsync(x => !x.IsRead, ct));

    public async Task ReadAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var notification = await Mine(user).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("notification", id);
        if (notification.IsRead) return;
        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReadAllAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var unread = await Mine(user).Where(x => !x.IsRead).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<EmailPreferences> PreferencesAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var contacts = await ContactsAsync(user, ct);
        return new EmailPreferences(contacts.All(x => x.EmailEnabled));
    }

    public async Task SetPreferencesAsync(ClaimsPrincipal user, EmailPreferences request, CancellationToken ct)
    {
        var contacts = await ContactsAsync(user, ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var contact in contacts)
        {
            contact.EmailEnabled = request.EmailEnabled;
            contact.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<AnnouncementResponse> SendAnnouncementAsync(AnnouncementRequest request, CancellationToken ct)
    {
        ValidateAnnouncement(request);

        IQueryable<Contact> query = db.Contacts.AsQueryable();
        switch (request.Audience)
        {
            case AnnouncementAudience.AllStudents:
                query = query.Where(c => c.PersonType == "STUDENT");
                break;
            case AnnouncementAudience.AllLecturers:
                query = query.Where(c => c.PersonType == "LECTURER");
                break;
            case AnnouncementAudience.AllMentors:
                query = query.Where(c => c.PersonType == "MENTOR");
                break;
            case AnnouncementAudience.Everyone:
                // All contacts
                break;
            case AnnouncementAudience.StudentsWithoutTeam:
            case AnnouncementAudience.TeamsWithoutMentor:
                // Blocker: Cần API nội bộ từ Team Service để xác định danh sách recipient mà không đọc chéo database.
                return new AnnouncementResponse(0);
            default:
                return new AnnouncementResponse(0);
        }

        var contacts = await query.ToListAsync(ct);
        if (contacts.Count == 0)
        {
            return new AnnouncementResponse(0);
        }

        var now = DateTimeOffset.UtcNow;
        var sourceEventId = Guid.NewGuid();

        foreach (var contact in contacts)
        {
            var notification = new UserNotification
            {
                Id = Guid.NewGuid(),
                RecipientType = contact.PersonType,
                RecipientId = contact.Id,
                Type = "Announcement",
                Title = request.Title.Trim(),
                Content = request.Content.Trim(),
                LinkUrl = null,
                SourceEventId = sourceEventId,
                IsRead = false,
                CreatedAt = now
            };
            db.Notifications.Add(notification);

            if (request.SendEmail && contact.EmailEnabled && !string.IsNullOrWhiteSpace(contact.Email))
            {
                db.EmailLogs.Add(new EmailLog
                {
                    Id = Guid.NewGuid(),
                    NotificationId = notification.Id,
                    SourceEventId = sourceEventId,
                    ToEmail = contact.Email,
                    Subject = request.Title.Trim(),
                    TemplateCode = "Announcement",
                    Status = "PENDING",
                    Attempts = 0,
                    CreatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return new AnnouncementResponse(contacts.Count);
    }

    private static void ValidateAnnouncement(AnnouncementRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Audience) || !AnnouncementAudience.All.Contains(request.Audience))
        {
            errors["audience"] = ["Đối tượng nhận thông báo không hợp lệ."];
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
        {
            errors["title"] = ["Tiêu đề phải từ 1 đến 200 ký tự."];
        }

        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
        {
            errors["content"] = ["Nội dung phải từ 1 đến 2000 ký tự."];
        }

        if (request.CourseCode is not null && request.CourseCode != "EXE101" && request.CourseCode != "EXE201")
        {
            errors["courseCode"] = ["Môn học phải là EXE101 hoặc EXE201."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private IQueryable<UserNotification> Mine(ClaimsPrincipal user)
    {
        var student = user.GetStudentId();
        var lecturer = user.GetLecturerId();
        var mentor = user.GetMentorId();
        var admin = user.IsInRole(Roles.Admin);
        return db.Notifications.Where(x =>
            (student.HasValue && x.RecipientType == "STUDENT" && x.RecipientId == student) ||
            (lecturer.HasValue && x.RecipientType == "LECTURER" && x.RecipientId == lecturer) ||
            (mentor.HasValue && x.RecipientType == "MENTOR" && x.RecipientId == mentor) ||
            (admin && x.RecipientType == "ADMIN" && x.RecipientId == null));
    }

    private async Task<List<Contact>> ContactsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var ids = new[] { user.GetStudentId(), user.GetLecturerId(), user.GetMentorId() }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (ids.Length == 0) throw new ForbiddenException("preferences-unavailable", "Tài khoản không có cài đặt email");
        var contacts = await db.Contacts.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (contacts.Count == 0) throw new NotFoundException("contact", ids[0]);
        return contacts;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1 || page > int.MaxValue / Math.Max(pageSize, 1)) errors["page"] = ["Trang phải từ 1 trở lên."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Số mục mỗi trang phải từ 1 đến 100."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
}
