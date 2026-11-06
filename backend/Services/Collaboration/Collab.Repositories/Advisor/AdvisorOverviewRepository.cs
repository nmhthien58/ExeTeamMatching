using Collab.Repositories.Context;
using Collab.Repositories.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Advisor;

/// <summary>Họp đã chốt kèm khung đã chốt và số khung đã đề xuất.</summary>
public sealed record ScheduledMeetingRow(Meeting Meeting, MeetingSlot ConfirmedSlot, int SlotCount);

/// <summary>Truy vấn chỉ đọc cho tổng quan cố vấn L01, M01 (BE-86). Không ghi dữ liệu.</summary>
public interface IAdvisorOverviewRepository
{
    /// <summary>Họp <c>SCHEDULED</c> của các workspace mà khung đã chốt chưa kết thúc ở <paramref name="now"/>, sớm nhất trước.</summary>
    Task<List<ScheduledMeetingRow>> ListScheduledNotEndedAsync(IReadOnlyCollection<Guid> workspaceIds, DateTimeOffset now, CancellationToken ct);

    /// <summary>Số họp <c>PROPOSED</c> ở các workspace do một trong <paramref name="organizers"/> tổ chức.</summary>
    Task<int> CountProposedByOrganizersAsync(IReadOnlyCollection<Guid> workspaceIds, IReadOnlyCollection<(string Type, Guid Id)> organizers, CancellationToken ct);
}

public sealed class AdvisorOverviewRepository(CollabDbContext db) : IAdvisorOverviewRepository
{
    /// <summary>Khung họp dài nhất (CHECK <c>ck_meeting_slots_duration</c>): lọc thô ở DB, lọc giờ kết thúc ở bộ nhớ.</summary>
    private const int MaxSlotMinutes = 240;

    public async Task<List<ScheduledMeetingRow>> ListScheduledNotEndedAsync(IReadOnlyCollection<Guid> workspaceIds, DateTimeOffset now, CancellationToken ct)
    {
        var earliestStart = now.AddMinutes(-MaxSlotMinutes);
        var rows = await (from m in db.Set<Meeting>().AsNoTracking()
                          join s in db.Set<MeetingSlot>().AsNoTracking() on m.ConfirmedSlotId equals s.Id
                          where workspaceIds.Contains(m.WorkspaceId) && m.Status == MeetingStatuses.Scheduled && s.StartsAt > earliestStart
                          select new { Meeting = m, Slot = s }).ToListAsync(ct);
        rows = rows.Where(r => r.Slot.StartsAt.AddMinutes(r.Slot.DurationMinutes) > now).OrderBy(r => r.Slot.StartsAt).ToList();

        var ids = rows.Select(r => r.Meeting.Id).ToList();
        var slotCounts = await db.Set<MeetingSlot>().AsNoTracking()
            .Where(s => ids.Contains(s.MeetingId))
            .GroupBy(s => s.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count, ct);
        return rows.Select(r => new ScheduledMeetingRow(r.Meeting, r.Slot, slotCounts.GetValueOrDefault(r.Meeting.Id))).ToList();
    }

    public async Task<int> CountProposedByOrganizersAsync(
        IReadOnlyCollection<Guid> workspaceIds, IReadOnlyCollection<(string Type, Guid Id)> organizers, CancellationToken ct)
    {
        var ids = organizers.Select(o => o.Id).ToList();
        var rows = await db.Set<Meeting>().AsNoTracking()
            .Where(m => workspaceIds.Contains(m.WorkspaceId) && m.Status == MeetingStatuses.Proposed && ids.Contains(m.OrganizerId))
            .Select(m => new { m.OrganizerType, m.OrganizerId })
            .ToListAsync(ct);
        return rows.Count(r => organizers.Contains((r.OrganizerType, r.OrganizerId)));
    }
}
