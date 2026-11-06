using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Collab.Repositories.Meetings;

/// <summary>Giá trị cột <c>status</c> (CHECK <c>ck_meeting_schedules_status</c>).</summary>
public static class ScheduleStatuses
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
    public const string Closed = "CLOSED";
}

/// <summary>Giá trị cột <c>source</c> (CHECK <c>ck_slot_bookings_source</c>).</summary>
public static class BookingSources
{
    public const string Leader = "LEADER";
    public const string Instructor = "INSTRUCTOR";
    public const string Auto = "AUTO";
}

public interface IMeetingScheduleRepository
{
    /// <summary>Học kỳ mới nhất có nhóm EXE201 giảng viên hướng dẫn; null khi chưa có nhóm nào.</summary>
    Task<Guid?> FindLatestSemesterAsync(Guid lecturerId, CancellationToken ct);

    /// <summary>Workspace EXE201 chưa lưu trữ giảng viên đang hướng dẫn trong học kỳ, sắp theo tên nhóm.</summary>
    Task<IReadOnlyList<Workspace>> ListInstructedAsync(Guid lecturerId, Guid semesterId, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<MeetingSchedule?> FindAsync(Guid lecturerId, Guid semesterId, CancellationToken ct);

    /// <summary>Lịch đã đăng tới hạn chọn slot, chưa đóng. Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<MeetingSchedule>> ListDueAsync(DateTimeOffset now, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<ScheduleWindow>> ListWindowsAsync(Guid scheduleId, CancellationToken ct);

    /// <summary>Slot của lịch, sắp theo thứ và giờ. Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<ScheduleSlot>> ListSlotsAsync(Guid scheduleId, CancellationToken ct);

    /// <summary>Booking còn hiệu lực của lịch. Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<SlotBooking>> ListBookingsAsync(Guid scheduleId, CancellationToken ct);

    /// <summary>Họp hằng tuần của booking kèm khung giờ đã chốt (nếu có). Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<(Meeting Meeting, MeetingSlot? Slot)>> ListWeeklyMeetingsAsync(Guid bookingId, CancellationToken ct);

    void Add(MeetingSchedule schedule);

    void Add(ScheduleWindow window);

    void Add(ScheduleSlot slot);

    void Add(SlotBooking booking);

    void Remove(ScheduleWindow window);

    void Remove(ScheduleSlot slot);

    /// <summary>Ghi <c>MeetingSlotsPublished</c> vào outbox (topic <c>exe.collab</c>, key = id lịch); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddPublishedEvent<TPayload>(Guid scheduleId, TPayload payload);

    /// <summary>Ghi <c>MeetingSlotBooked</c> vào outbox (topic <c>exe.collab</c>, key = id workspace); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddBookedEvent<TPayload>(Guid workspaceId, TPayload payload);

    Task SaveAsync(CancellationToken ct);

    /// <summary>
    /// Lưu thay đổi booking; trả <c>false</c> khi DB chặn vì slot vừa có nhóm khác đặt
    /// (<c>uq_slot_bookings_slot_active</c>, <c>uq_slot_bookings_workspace_active</c>) – chống hai nhóm bấm cùng lúc.
    /// </summary>
    Task<bool> TrySaveBookingAsync(CancellationToken ct);
}

public sealed class MeetingScheduleRepository(CollabDbContext db) : IMeetingScheduleRepository
{
    private const string Exe201 = "EXE201";

    private IQueryable<Workspace> Instructed(Guid lecturerId) =>
        from w in db.Set<Workspace>().AsNoTracking()
        join m in db.Set<WorkspaceMember>() on w.Id equals m.WorkspaceId
        where m.PersonType == PersonTypes.Lecturer && m.PersonId == lecturerId && m.Role == WorkspaceRoles.Instructor && m.LeftAt == null
            && w.CourseCode == Exe201 && w.ArchivedAt == null
        select w;

    public async Task<Guid?> FindLatestSemesterAsync(Guid lecturerId, CancellationToken ct) =>
        await Instructed(lecturerId).OrderByDescending(w => w.CreatedAt).Select(w => (Guid?)w.SemesterId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Workspace>> ListInstructedAsync(Guid lecturerId, Guid semesterId, CancellationToken ct) =>
        await Instructed(lecturerId).Where(w => w.SemesterId == semesterId).OrderBy(w => w.TeamName).ThenBy(w => w.Id).ToListAsync(ct);

    public Task<MeetingSchedule?> FindAsync(Guid lecturerId, Guid semesterId, CancellationToken ct) =>
        db.Set<MeetingSchedule>().FirstOrDefaultAsync(s => s.LecturerId == lecturerId && s.SemesterId == semesterId && s.CourseCode == Exe201, ct);

    public async Task<IReadOnlyList<MeetingSchedule>> ListDueAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.Set<MeetingSchedule>().Where(s => s.Status == ScheduleStatuses.Published && s.BookingDeadline <= now).ToListAsync(ct);

    public async Task<IReadOnlyList<ScheduleWindow>> ListWindowsAsync(Guid scheduleId, CancellationToken ct) =>
        await db.Set<ScheduleWindow>().Where(w => w.ScheduleId == scheduleId).OrderBy(w => w.DayOfWeek).ThenBy(w => w.StartTime).ToListAsync(ct);

    public async Task<IReadOnlyList<ScheduleSlot>> ListSlotsAsync(Guid scheduleId, CancellationToken ct) =>
        await db.Set<ScheduleSlot>().Where(s => s.ScheduleId == scheduleId).OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync(ct);

    public async Task<IReadOnlyList<SlotBooking>> ListBookingsAsync(Guid scheduleId, CancellationToken ct) =>
        await db.Set<SlotBooking>().Where(b => b.ScheduleId == scheduleId && b.CancelledAt == null).ToListAsync(ct);

    public async Task<IReadOnlyList<(Meeting Meeting, MeetingSlot? Slot)>> ListWeeklyMeetingsAsync(Guid bookingId, CancellationToken ct)
    {
        var rows = await db.Set<Meeting>().Where(m => m.ScheduleBookingId == bookingId).OrderBy(m => m.WeekNo).ToListAsync(ct);
        var slotIds = rows.Where(m => m.ConfirmedSlotId is not null).Select(m => m.ConfirmedSlotId!.Value).ToList();
        var slots = await db.Set<MeetingSlot>().Where(s => slotIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        return rows.Select(m => (m, m.ConfirmedSlotId is { } id ? slots.GetValueOrDefault(id) : null)).ToList();
    }

    public void Add(MeetingSchedule schedule) => db.Set<MeetingSchedule>().Add(schedule);

    public void Add(ScheduleWindow window) => db.Set<ScheduleWindow>().Add(window);

    public void Add(ScheduleSlot slot) => db.Set<ScheduleSlot>().Add(slot);

    public void Add(SlotBooking booking) => db.Set<SlotBooking>().Add(booking);

    public void Remove(ScheduleWindow window) => db.Set<ScheduleWindow>().Remove(window);

    public void Remove(ScheduleSlot slot) => db.Set<ScheduleSlot>().Remove(slot);

    public void AddPublishedEvent<TPayload>(Guid scheduleId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, "MeetingSlotsPublished", "MeetingSchedule", scheduleId, payload);

    public void AddBookedEvent<TPayload>(Guid workspaceId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, "MeetingSlotBooked", "Workspace", workspaceId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<bool> TrySaveBookingAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}
