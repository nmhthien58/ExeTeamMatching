using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Meetings;

/// <summary>Giá trị cột <c>status</c> (CHECK <c>ck_meetings_status</c>).</summary>
public static class MeetingStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Scheduled = "SCHEDULED";
    public const string Cancelled = "CANCELLED";
    public const string Done = "DONE";
}

/// <summary>Giá trị cột <c>answer</c> (CHECK <c>ck_meeting_responses_answer</c>).</summary>
public static class MeetingAnswers
{
    public const string Yes = "YES";
    public const string No = "NO";
}

public interface IMeetingRepository
{
    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Meeting?> FindAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Họp của workspace, lọc theo trạng thái nếu có.</summary>
    Task<IReadOnlyList<Meeting>> ListAsync(Guid workspaceId, string? status, CancellationToken ct);

    Task<int> CountDoneMentorSessionsAsync(Guid workspaceId, CancellationToken ct);

    /// <summary>Khung giờ của các cuộc họp, sắp theo giờ bắt đầu.</summary>
    Task<IReadOnlyList<MeetingSlot>> ListSlotsAsync(IReadOnlyCollection<Guid> meetingIds, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<MeetingSlot?> FindSlotAsync(Guid slotId, CancellationToken ct);

    /// <summary>Câu trả lời của các khung. Có theo dõi thay đổi, để sửa câu trả lời cũ.</summary>
    Task<IReadOnlyList<MeetingRespons>> ListResponsesAsync(IReadOnlyCollection<Guid> slotIds, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<MeetingMinutes?> FindMinutesAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Id việc tạo từ biên bản của cuộc họp.</summary>
    Task<IReadOnlyList<Guid>> ListSourceTaskIdsAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Họp đã chốt bắt đầu trong (<paramref name="after"/>, <paramref name="until"/>] mà chưa gửi mốc nhắc này.</summary>
    Task<IReadOnlyList<(Meeting Meeting, DateTimeOffset StartsAt)>> ListUpcomingAsync(DateTimeOffset after, DateTimeOffset until, short offsetMinutes, CancellationToken ct);

    Task<bool> ReminderSentAsync(Guid meetingId, short offsetMinutes, CancellationToken ct);

    void Add(Meeting meeting, IEnumerable<MeetingSlot> slots);

    void Add(MeetingSlot slot);

    void Add(MeetingRespons response);

    void Add(MeetingMinutes minutes);

    void Add(MeetingReminder reminder);

    /// <summary>Xóa các mốc nhắc đã gửi; lưu cùng lần <see cref="SaveAsync"/>.</summary>
    Task RemoveRemindersAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.collab</c>, key = id workspace); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload);

    Task SaveAsync(CancellationToken ct);

    /// <summary>
    /// Lưu cuộc họp một khung: <c>meetings</c> và <c>meeting_slots</c> tham chiếu nhau nên phải ghi họp và khung trước, chạy
    /// <paramref name="confirm"/> rồi ghi lần hai, trong cùng transaction.
    /// </summary>
    Task SaveConfirmedAsync(Action confirm, CancellationToken ct);
}

public sealed class MeetingRepository(CollabDbContext db) : IMeetingRepository
{
    public Task<Meeting?> FindAsync(Guid meetingId, CancellationToken ct) =>
        db.Set<Meeting>().FirstOrDefaultAsync(m => m.Id == meetingId, ct);

    public async Task<IReadOnlyList<Meeting>> ListAsync(Guid workspaceId, string? status, CancellationToken ct)
    {
        var query = db.Set<Meeting>().AsNoTracking().Where(m => m.WorkspaceId == workspaceId);
        if (status is not null)
        {
            query = query.Where(m => m.Status == status);
        }
        return await query.ToListAsync(ct);
    }

    public Task<int> CountDoneMentorSessionsAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<Meeting>().CountAsync(m => m.WorkspaceId == workspaceId && m.IsMentorSession && m.Status == MeetingStatuses.Done, ct);

    public async Task<IReadOnlyList<MeetingSlot>> ListSlotsAsync(IReadOnlyCollection<Guid> meetingIds, CancellationToken ct) =>
        await db.Set<MeetingSlot>().AsNoTracking().Where(s => meetingIds.Contains(s.MeetingId)).OrderBy(s => s.StartsAt).ToListAsync(ct);

    public Task<MeetingSlot?> FindSlotAsync(Guid slotId, CancellationToken ct) =>
        db.Set<MeetingSlot>().FirstOrDefaultAsync(s => s.Id == slotId, ct);

    public async Task<IReadOnlyList<MeetingRespons>> ListResponsesAsync(IReadOnlyCollection<Guid> slotIds, CancellationToken ct) =>
        await db.Set<MeetingRespons>().Where(r => slotIds.Contains(r.SlotId)).ToListAsync(ct);

    public Task<MeetingMinutes?> FindMinutesAsync(Guid meetingId, CancellationToken ct) =>
        db.Set<MeetingMinutes>().FirstOrDefaultAsync(m => m.MeetingId == meetingId, ct);

    public async Task<IReadOnlyList<Guid>> ListSourceTaskIdsAsync(Guid meetingId, CancellationToken ct) =>
        await db.Set<TaskItem>().AsNoTracking().Where(t => t.SourceMeetingId == meetingId).Select(t => t.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<(Meeting Meeting, DateTimeOffset StartsAt)>> ListUpcomingAsync(DateTimeOffset after, DateTimeOffset until, short offsetMinutes, CancellationToken ct)
    {
        var rows = await (from m in db.Set<Meeting>().AsNoTracking()
                          join s in db.Set<MeetingSlot>() on m.ConfirmedSlotId equals s.Id
                          where m.Status == MeetingStatuses.Scheduled && s.StartsAt > after && s.StartsAt <= until
                              && !db.Set<MeetingReminder>().Any(r => r.MeetingId == m.Id && r.OffsetMinutes == offsetMinutes)
                          orderby s.StartsAt
                          select new { Meeting = m, s.StartsAt }).ToListAsync(ct);
        return rows.Select(r => (r.Meeting, r.StartsAt)).ToList();
    }

    public Task<bool> ReminderSentAsync(Guid meetingId, short offsetMinutes, CancellationToken ct) =>
        db.Set<MeetingReminder>().AnyAsync(r => r.MeetingId == meetingId && r.OffsetMinutes == offsetMinutes, ct);

    public void Add(Meeting meeting, IEnumerable<MeetingSlot> slots)
    {
        db.Set<Meeting>().Add(meeting);
        db.Set<MeetingSlot>().AddRange(slots);
    }

    public void Add(MeetingSlot slot) => db.Set<MeetingSlot>().Add(slot);

    public void Add(MeetingRespons response) => db.Set<MeetingRespons>().Add(response);

    public void Add(MeetingMinutes minutes) => db.Set<MeetingMinutes>().Add(minutes);

    public void Add(MeetingReminder reminder) => db.Set<MeetingReminder>().Add(reminder);

    public async Task RemoveRemindersAsync(Guid meetingId, CancellationToken ct) =>
        db.Set<MeetingReminder>().RemoveRange(await db.Set<MeetingReminder>().Where(r => r.MeetingId == meetingId).ToListAsync(ct));

    public void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, eventType, "Workspace", workspaceId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task SaveConfirmedAsync(Action confirm, CancellationToken ct)
    {
        // Test dùng DB InMemory không có transaction.
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        confirm();
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
    }
}
