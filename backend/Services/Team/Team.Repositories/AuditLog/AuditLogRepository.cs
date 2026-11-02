using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;

namespace Team.Repositories.AuditLog;

/// <summary>Bộ lọc nhật ký; các trường null không lọc. <c>From</c>, <c>To</c> tính cả hai đầu.</summary>
public sealed record AuditLogFilter(Guid? TeamId, Guid? ActorAccountId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize, string? ActorName = null);

public interface IAuditLogRepository
{
    /// <summary>Mới nhất trước.</summary>
    Task<(List<AuditLogEntry> Items, int TotalCount)> SearchAsync(AuditLogFilter filter, CancellationToken ct);

    /// <summary>Giảng viên hướng dẫn hoặc mentor đang được gán cho nhóm (L05).</summary>
    Task<bool> IsAdvisorAsync(Guid teamId, Guid? lecturerId, Guid? mentorId, CancellationToken ct);
}

public sealed class AuditLogRepository(TeamDbContext db) : IAuditLogRepository
{
    public async Task<(List<AuditLogEntry> Items, int TotalCount)> SearchAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var query = db.Set<AuditLogEntry>().AsNoTracking();
        if (filter.TeamId is { } teamId)
        {
            query = query.Where(e => e.TeamId == teamId);
        }
        if (filter.ActorAccountId is { } actorId)
        {
            query = query.Where(e => e.ActorAccountId == actorId);
        }
        if (!string.IsNullOrWhiteSpace(filter.ActorName))
        {
            var pattern = "%" + filter.ActorName.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(e => e.ActorName != null && EF.Functions.ILike(e.ActorName, pattern));
        }
        if (filter.From is { } from)
        {
            query = query.Where(e => e.OccurredAt >= from);
        }
        if (filter.To is { } to)
        {
            query = query.Where(e => e.OccurredAt <= to);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> IsAdvisorAsync(Guid teamId, Guid? lecturerId, Guid? mentorId, CancellationToken ct) =>
        (lecturerId is not null
            && await db.Set<LecturerAssignment>().AnyAsync(a => a.TeamId == teamId && a.LecturerId == lecturerId && a.Status == "ASSIGNED", ct))
        || (mentorId is not null
            && await db.Set<MentorAssignment>().AnyAsync(a => a.TeamId == teamId && a.MentorId == mentorId && a.Status == "ASSIGNED", ct));
}
