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
        var contact = await ContactAsync(user, ct);
        return new EmailPreferences(contact.EmailEnabled);
    }

    public async Task SetPreferencesAsync(ClaimsPrincipal user, EmailPreferences request, CancellationToken ct)
    {
        var contact = await ContactAsync(user, ct);
        contact.EmailEnabled = request.EmailEnabled;
        contact.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
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

    private async Task<Contact> ContactAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var id = user.GetStudentId() ?? user.GetLecturerId() ?? user.GetMentorId();
        if (id is null) throw new ForbiddenException("preferences-unavailable", "Tài khoản không có cài đặt email");
        return await db.Contacts.FindAsync([id.Value], ct) ?? throw new NotFoundException("contact", id);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1 || page > int.MaxValue / Math.Max(pageSize, 1)) errors["page"] = ["Trang phải từ 1 trở lên."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Số mục mỗi trang phải từ 1 đến 100."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
}
