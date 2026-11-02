using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Meetings;
using Collab.Repositories.Workspaces;
using Collab.Services.Realtime;
using Collab.Services.Workspaces;

namespace Collab.Services.Meetings;

public interface IMeetingScheduleService
{
    /// <summary>Lịch của giảng viên trong học kỳ mới nhất có nhóm EXE201; null khi chưa tạo.</summary>
    Task<MeetingScheduleResponse?> GetMineAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Tạo hoặc sửa lịch, cắt lại slot; slot đã có nhóm không bị xóa (409 <c>slot-booked</c>).</summary>
    Task<MeetingScheduleResponse> SaveAsync(MeetingScheduleInput input, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingScheduleResponse> PublishAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Giảng viên đổi slot cho nhóm, kể cả sau hạn; họp tuần chưa diễn ra dời theo.</summary>
    Task<MeetingScheduleResponse> MoveAsync(Guid workspaceId, SlotBookingRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Lịch của nhóm (C09); null khi không phải nhóm EXE201 hoặc giảng viên chưa đăng.</summary>
    Task<TeamMeetingScheduleResponse?> GetForTeamAsync(Guid workspaceId, WorkspaceAccess viewer, CancellationToken ct);

    /// <summary>Trưởng nhóm đặt / đổi slot tới hạn; ai đặt trước được trước.</summary>
    Task<TeamMeetingScheduleResponse> BookAsync(Guid workspaceId, SlotBookingRequest request, WorkspaceAccess writer, CancellationToken ct);

    /// <summary>Tới hạn: xếp nhóm chưa đặt vào slot trống, sinh họp hằng tuần, đóng lịch. Gọi lại không tạo trùng.</summary>
    Task<CloseBookingResponse> CloseBookingAsync(CancellationToken ct);
}

/// <summary>
/// Lịch họp hằng tuần EXE201 (BE-82, C09, rule 25). Giảng viên đăng khung giờ → hệ thống cắt slot → trưởng nhóm chọn tới hạn →
/// hết hạn xếp nhóm chưa chọn → sinh một họp mỗi tuần tới hết học kỳ. DRAFT → PUBLISHED → CLOSED.
/// Mỗi nhóm giữ một dòng <c>slot_bookings</c>, đổi slot là sửa dòng đó; hai nhóm đặt cùng slot thì DB chặn bằng chỉ mục duy nhất.
/// Sự kiện qua outbox; <c>slotsChanged</c> đẩy sau khi lưu.
/// </summary>
public sealed class MeetingScheduleService(
    IMeetingScheduleRepository schedules,
    IMeetingRepository meetings,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IScheduleSemesterClient semesters,
    IRealtimeNotifier realtime,
    TimeProvider clock) : IMeetingScheduleService
{
    public async Task<MeetingScheduleResponse?> GetMineAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturerId = LecturerId(user);
        if (await schedules.FindLatestSemesterAsync(lecturerId, ct) is not { } semesterId
            || await schedules.FindAsync(lecturerId, semesterId, ct) is not { } schedule)
        {
            return null;
        }
        return await ToResponseAsync(schedule, ct);
    }

    public async Task<MeetingScheduleResponse> SaveAsync(MeetingScheduleInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturerId = LecturerId(user);
        var semesterId = await schedules.FindLatestSemesterAsync(lecturerId, ct) ?? throw MeetingScheduleErrors.NoTeams();
        var semester = await semesters.GetAsync(semesterId, ct);
        var now = clock.GetUtcNow();

        var schedule = await schedules.FindAsync(lecturerId, semesterId, ct);
        var isNew = schedule is null;
        if (schedule is null)
        {
            var teams = await schedules.ListInstructedAsync(lecturerId, semesterId, ct);
            schedule = new MeetingSchedule
            {
                Id = Guid.NewGuid(),
                LecturerId = lecturerId,
                LecturerName = await LecturerNameAsync(teams[0].Id, lecturerId, ct),
                SemesterId = semesterId,
                CourseCode = "EXE201",
                Status = ScheduleStatuses.Draft,
                SlotMinutes = (short)(input.SlotMinutes ?? semester?.MeetingSlotMinutes ?? ScheduleRules.DefaultSlotMinutes),
                CreatedAt = now,
                UpdatedAt = now,
            };
        }
        else
        {
            schedule.SlotMinutes = (short)(input.SlotMinutes ?? schedule.SlotMinutes);
        }
        schedule.Location = input.Location!.Trim();
        if (schedule.Status == ScheduleStatuses.Draft)
        {
            schedule.BookingDeadline = semester is null
                ? now.AddDays(ScheduleRules.FallbackDeadlineDays)
                : ScheduleRules.BookingDeadline(semester.StartDate, semester.SlotBookingDeadlineWeek);
        }

        await ReplaceWindowsAsync(schedule, ScheduleRules.ParseWindows(input.Windows!, schedule.SlotMinutes), isNew, ct);
        if (isNew)
        {
            schedules.Add(schedule);
        }
        await schedules.SaveAsync(ct);
        if (schedule.Status != ScheduleStatuses.Draft)
        {
            await NotifySlotsChangedAsync(schedule, ct);
        }
        return await ToResponseAsync(schedule, ct);
    }

    public async Task<MeetingScheduleResponse> PublishAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturerId = LecturerId(user);
        var semesterId = await schedules.FindLatestSemesterAsync(lecturerId, ct) ?? throw MeetingScheduleErrors.NoTeams();
        var schedule = await schedules.FindAsync(lecturerId, semesterId, ct) ?? throw new NotFoundException("lịch họp hằng tuần");
        if (schedule.Status != ScheduleStatuses.Draft)
        {
            return await ToResponseAsync(schedule, ct);
        }

        var slots = await schedules.ListSlotsAsync(schedule.Id, ct);
        if (slots.Count == 0)
        {
            throw MeetingScheduleErrors.NoSlots();
        }
        schedule.Status = ScheduleStatuses.Published;
        schedule.PublishedAt = clock.GetUtcNow();

        var teams = await schedules.ListInstructedAsync(lecturerId, semesterId, ct);
        var members = await lookup.ListMembersAsync(teams.Select(t => t.Id).ToList(), ct);
        schedules.AddPublishedEvent(schedule.Id, new MeetingSlotsPublishedEvent(
            schedule.Id, schedule.LecturerId, schedule.LecturerName, schedule.BookingDeadline, slots.Count, Students(teams.SelectMany(t => members[t.Id]))));
        await schedules.SaveAsync(ct);
        return await ToResponseAsync(schedule, ct);
    }

    public async Task<MeetingScheduleResponse> MoveAsync(Guid workspaceId, SlotBookingRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var lecturerId = LecturerId(user);
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var teams = await schedules.ListInstructedAsync(lecturerId, workspace.SemesterId, ct);
        if (teams.All(t => t.Id != workspaceId))
        {
            throw new ForbiddenException("not-workspace-advisor", "Bạn không phải cố vấn của nhóm này");
        }
        var schedule = await schedules.FindAsync(lecturerId, workspace.SemesterId, ct) ?? throw new NotFoundException("lịch họp hằng tuần");
        if (schedule.Status == ScheduleStatuses.Draft)
        {
            throw MeetingScheduleErrors.BookingClosed();
        }

        var booking = await AssignAsync(schedule, workspaceId, request.SlotId, BookingSources.Instructor, lecturerId, ct);
        if (booking is not null && schedule.Status == ScheduleStatuses.Closed)
        {
            var semester = await semesters.GetAsync(schedule.SemesterId, ct) ?? throw MeetingScheduleErrors.SemesterUnavailable(schedule.SemesterId);
            var slot = (await schedules.ListSlotsAsync(schedule.Id, ct)).Single(s => s.Id == booking.SlotId);
            await MoveWeeklyMeetingsAsync(schedule, booking, slot, semester, ct);
            await CreateWeeklyMeetingsAsync(schedule, booking, slot, semester, ct);
        }
        await NotifySlotsChangedAsync(schedule, ct);
        return await ToResponseAsync(schedule, ct);
    }

    public async Task<TeamMeetingScheduleResponse?> GetForTeamAsync(Guid workspaceId, WorkspaceAccess viewer, CancellationToken ct)
    {
        var schedule = await FindForTeamAsync(workspaceId, ct);
        return schedule is null ? null : await ToTeamResponseAsync(schedule, workspaceId, viewer, ct);
    }

    public async Task<TeamMeetingScheduleResponse> BookAsync(Guid workspaceId, SlotBookingRequest request, WorkspaceAccess writer, CancellationToken ct)
    {
        if (!writer.IsLeader)
        {
            throw MeetingScheduleErrors.NotLeader();
        }
        var schedule = await FindForTeamAsync(workspaceId, ct) ?? throw new NotFoundException("lịch họp hằng tuần");
        if (!CanBook(schedule))
        {
            throw MeetingScheduleErrors.BookingClosed();
        }
        await AssignAsync(schedule, workspaceId, request.SlotId, BookingSources.Leader, writer.PersonId, ct);
        await NotifySlotsChangedAsync(schedule, ct);
        return await ToTeamResponseAsync(schedule, workspaceId, writer, ct);
    }

    public async Task<CloseBookingResponse> CloseBookingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        int autoBooked = 0, teamsWithoutSlot = 0, created = 0;
        foreach (var schedule in await schedules.ListDueAsync(now, ct))
        {
            // Đọc học kỳ trước khi ghi: thiếu thì để Job thử lại, không đóng lịch mà chưa có họp.
            var semester = await semesters.GetAsync(schedule.SemesterId, ct) ?? throw MeetingScheduleErrors.SemesterUnavailable(schedule.SemesterId);
            var teams = await schedules.ListInstructedAsync(schedule.LecturerId, schedule.SemesterId, ct);
            var slots = await schedules.ListSlotsAsync(schedule.Id, ct);
            var bookings = (await schedules.ListBookingsAsync(schedule.Id, ct)).ToList();

            var taken = bookings.Select(b => b.SlotId).ToHashSet();
            var free = new Queue<ScheduleSlot>(slots.Where(s => !taken.Contains(s.Id)));
            var waiting = teams.Where(t => bookings.All(b => b.WorkspaceId != t.Id)).ToList();
            var members = await lookup.ListMembersAsync(teams.Select(t => t.Id).ToList(), ct);
            foreach (var team in waiting)
            {
                if (!free.TryDequeue(out var slot))
                {
                    teamsWithoutSlot++;
                    continue;
                }
                var booking = new SlotBooking
                {
                    Id = Guid.NewGuid(), ScheduleId = schedule.Id, SlotId = slot.Id, WorkspaceId = team.Id,
                    Source = BookingSources.Auto, BookedAt = now,
                };
                schedules.Add(booking);
                bookings.Add(booking);
                schedules.AddBookedEvent(team.Id, BookedEvent(schedule, booking, slot, Students(members[team.Id])));
                autoBooked++;
            }
            if (!await schedules.TrySaveBookingAsync(ct))
            {
                throw MeetingScheduleErrors.SlotTaken(); // giảng viên vừa đổi slot; Job thử lại với dữ liệu mới
            }

            foreach (var booking in bookings)
            {
                created += await CreateWeeklyMeetingsAsync(schedule, booking, slots.Single(s => s.Id == booking.SlotId), semester, ct);
            }
            schedule.Status = ScheduleStatuses.Closed;
            schedule.ClosedAt = now;
            await schedules.SaveAsync(ct);
            await NotifySlotsChangedAsync(schedule, ct);
        }
        return new CloseBookingResponse(autoBooked, teamsWithoutSlot, created);
    }

    /// <summary>
    /// Đặt nhóm vào slot, trả booking; null khi nhóm đã ở đúng slot này. Nhóm đã có booking thì sửa dòng đó để họp đã sinh vẫn gắn với nó.
    /// </summary>
    private async Task<SlotBooking?> AssignAsync(MeetingSchedule schedule, Guid workspaceId, Guid slotId, string source, Guid bookedById, CancellationToken ct)
    {
        var slot = (await schedules.ListSlotsAsync(schedule.Id, ct)).FirstOrDefault(s => s.Id == slotId)
            ?? throw MeetingErrors.Invalid("slotId", "Slot không thuộc lịch này.");
        var bookings = await schedules.ListBookingsAsync(schedule.Id, ct);
        var booking = bookings.FirstOrDefault(b => b.WorkspaceId == workspaceId);
        if (booking?.SlotId == slotId)
        {
            return null;
        }
        if (bookings.Any(b => b.SlotId == slotId))
        {
            throw MeetingScheduleErrors.SlotTaken();
        }

        var now = clock.GetUtcNow();
        if (booking is null)
        {
            booking = new SlotBooking { Id = Guid.NewGuid(), ScheduleId = schedule.Id, WorkspaceId = workspaceId };
            schedules.Add(booking);
        }
        booking.SlotId = slotId;
        booking.Source = source;
        booking.BookedById = bookedById;
        booking.BookedAt = now;
        var recipients = Students(await lookup.ListMembersAsync(workspaceId, ct));
        schedules.AddBookedEvent(workspaceId, BookedEvent(schedule, booking, slot, recipients));
        if (!await schedules.TrySaveBookingAsync(ct))
        {
            throw MeetingScheduleErrors.SlotTaken();
        }
        return booking;
    }

    /// <summary>Sinh họp cho các tuần còn lại; tuần đã có dòng (kể cả đã hủy) không sinh lại.</summary>
    private async Task<int> CreateWeeklyMeetingsAsync(MeetingSchedule schedule, SlotBooking booking, ScheduleSlot slot, ScheduleSemester semester, CancellationToken ct)
    {
        var existing = (await schedules.ListWeeklyMeetingsAsync(booking.Id, ct)).Select(r => r.Meeting.WeekNo).ToHashSet();
        var now = clock.GetUtcNow();
        var weeks = ScheduleRules.Occurrences(semester.StartDate, semester.EndDate, slot.DayOfWeek, slot.StartTime, now)
            .Where(o => !existing.Contains(o.WeekNo)).ToList();
        if (weeks.Count == 0)
        {
            return 0;
        }

        var created = weeks.Select(week =>
        {
            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                WorkspaceId = booking.WorkspaceId,
                Title = $"Họp hằng tuần – Tuần {week.WeekNo}",
                Location = schedule.Location,
                OrganizerType = PersonTypes.Lecturer,
                OrganizerId = schedule.LecturerId,
                OrganizerName = schedule.LecturerName,
                OrganizerRole = WorkspaceRoles.Instructor,
                Status = MeetingStatuses.Proposed,
                ScheduleBookingId = booking.Id,
                WeekNo = week.WeekNo,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var meetingSlot = new MeetingSlot
            {
                Id = Guid.NewGuid(), MeetingId = meeting.Id, StartsAt = week.StartsAt, DurationMinutes = ScheduleRules.MeetingMinutes(schedule.SlotMinutes),
            };
            meetings.Add(meeting, [meetingSlot]);
            return (meeting, meetingSlot);
        }).ToList();

        // Không phát MeetingScheduled từng tuần: nhóm đã được báo bằng MeetingSlotBooked.
        await meetings.SaveConfirmedAsync(() =>
        {
            foreach (var (meeting, meetingSlot) in created)
            {
                meeting.Status = MeetingStatuses.Scheduled;
                meeting.ConfirmedSlotId = meetingSlot.Id;
            }
        }, ct);
        return created.Count;
    }

    /// <summary>
    /// Dời họp tuần chưa diễn ra sang slot mới, giữ nguyên tuần. Tuần mà giờ mới đã qua thì giữ giờ cũ để nhóm không mất buổi họp đó.
    /// </summary>
    private async Task MoveWeeklyMeetingsAsync(MeetingSchedule schedule, SlotBooking booking, ScheduleSlot slot, ScheduleSemester semester, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var (meeting, meetingSlot) in await schedules.ListWeeklyMeetingsAsync(booking.Id, ct))
        {
            if (meeting.Status != MeetingStatuses.Scheduled || meetingSlot is null || meetingSlot.StartsAt <= now)
            {
                continue;
            }
            var startsAt = ScheduleRules.InWeek(semester.StartDate, meeting.WeekNo!.Value, slot.DayOfWeek, slot.StartTime);
            if (startsAt > now)
            {
                meetingSlot.StartsAt = startsAt;
                meetingSlot.DurationMinutes = ScheduleRules.MeetingMinutes(schedule.SlotMinutes);
                meeting.UpdatedAt = now;
            }
        }
        await schedules.SaveAsync(ct);
    }

    /// <summary>
    /// Thay khung giờ và cắt lại slot. Slot trùng thứ và giờ bắt đầu được giữ để booking không mất; slot đã có nhóm mà bị bỏ hoặc đổi giờ
    /// kết thúc thì báo 409.
    /// </summary>
    private async Task ReplaceWindowsAsync(MeetingSchedule schedule, IReadOnlyList<(short Day, TimeOnly Start, TimeOnly End)> requested, bool isNew, CancellationToken ct)
    {
        var oldWindows = isNew ? [] : await schedules.ListWindowsAsync(schedule.Id, ct);
        var oldSlots = isNew ? [] : await schedules.ListSlotsAsync(schedule.Id, ct);
        var booked = isNew ? [] : (await schedules.ListBookingsAsync(schedule.Id, ct)).Select(b => b.SlotId).ToHashSet();

        var windows = requested.Select(r => oldWindows.FirstOrDefault(w => w.DayOfWeek == r.Day && w.StartTime == r.Start && w.EndTime == r.End)
            ?? new ScheduleWindow { Id = Guid.NewGuid(), ScheduleId = schedule.Id, DayOfWeek = r.Day, StartTime = r.Start, EndTime = r.End }).ToList();
        foreach (var window in windows.Where(w => !oldWindows.Contains(w)))
        {
            schedules.Add(window);
        }

        var wanted = windows.SelectMany(w => ScheduleRules.Cut(w.DayOfWeek, w.StartTime, w.EndTime, schedule.SlotMinutes).Select(c => (Window: w, Cut: c))).ToList();
        var kept = new HashSet<Guid>();
        foreach (var (window, cut) in wanted)
        {
            if (oldSlots.FirstOrDefault(s => s.DayOfWeek == cut.DayOfWeek && s.StartTime == cut.Start) is { } slot)
            {
                if (slot.EndTime != cut.End && booked.Contains(slot.Id))
                {
                    throw MeetingScheduleErrors.SlotBooked();
                }
                slot.EndTime = cut.End;
                slot.WindowId = window.Id;
                kept.Add(slot.Id);
            }
            else
            {
                schedules.Add(new ScheduleSlot
                {
                    Id = Guid.NewGuid(), ScheduleId = schedule.Id, WindowId = window.Id, DayOfWeek = cut.DayOfWeek, StartTime = cut.Start, EndTime = cut.End,
                });
            }
        }

        foreach (var slot in oldSlots.Where(s => !kept.Contains(s.Id)))
        {
            if (booked.Contains(slot.Id))
            {
                throw MeetingScheduleErrors.SlotBooked();
            }
            schedules.Remove(slot);
        }
        foreach (var window in oldWindows.Where(w => !windows.Contains(w)))
        {
            schedules.Remove(window);
        }
    }

    private bool CanBook(MeetingSchedule schedule) => schedule.Status == ScheduleStatuses.Published && clock.GetUtcNow() < schedule.BookingDeadline;

    /// <summary>Lịch đã đăng của giảng viên hướng dẫn nhóm.</summary>
    private async Task<MeetingSchedule?> FindForTeamAsync(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        if (workspace.CourseCode != "EXE201" || await workspaces.FindActiveByRoleAsync(workspaceId, WorkspaceRoles.Instructor, ct) is not { } instructor)
        {
            return null;
        }
        var schedule = await schedules.FindAsync(instructor.PersonId, workspace.SemesterId, ct);
        return schedule?.Status == ScheduleStatuses.Draft ? null : schedule;
    }

    private async Task<string> LecturerNameAsync(Guid workspaceId, Guid lecturerId, CancellationToken ct) =>
        (await workspaces.FindMemberAsync(workspaceId, PersonTypes.Lecturer, lecturerId, ct))?.DisplayName ?? "Giảng viên";

    private static Guid LecturerId(ClaimsPrincipal user) =>
        user.GetLecturerId() ?? throw new ForbiddenException(title: "Chỉ giảng viên có lịch họp hằng tuần");

    private static IReadOnlyList<MeetingInvitee> Students(IEnumerable<WorkspaceMember> members) =>
        members.Where(m => m.LeftAt is null && m.PersonType == PersonTypes.Student).Select(m => new MeetingInvitee(m.PersonType, m.PersonId)).ToList();

    private static MeetingSlotBookedEvent BookedEvent(MeetingSchedule schedule, SlotBooking booking, ScheduleSlot slot, IReadOnlyList<MeetingInvitee> recipients) => new(
        booking.WorkspaceId, schedule.Id, slot.Id, slot.DayOfWeek, ScheduleRules.Format(slot.StartTime), ScheduleRules.Format(slot.EndTime), booking.Source, recipients);

    private async Task NotifySlotsChangedAsync(MeetingSchedule schedule, CancellationToken ct)
    {
        foreach (var team in await schedules.ListInstructedAsync(schedule.LecturerId, schedule.SemesterId, ct))
        {
            await realtime.ToWorkspaceAsync(team.Id, HubEvents.SlotsChanged, new { scheduleId = schedule.Id }, ct);
        }
    }

    private async Task<MeetingScheduleResponse> ToResponseAsync(MeetingSchedule schedule, CancellationToken ct)
    {
        var windows = await schedules.ListWindowsAsync(schedule.Id, ct);
        var slots = await schedules.ListSlotsAsync(schedule.Id, ct);
        var bookings = (await schedules.ListBookingsAsync(schedule.Id, ct)).ToDictionary(b => b.SlotId);
        var teams = await schedules.ListInstructedAsync(schedule.LecturerId, schedule.SemesterId, ct);
        var names = teams.ToDictionary(t => t.Id, t => t.TeamName);
        var booked = bookings.Values.Select(b => b.WorkspaceId).ToHashSet();

        return new MeetingScheduleResponse(
            schedule.Id, schedule.Status, schedule.SlotMinutes, schedule.Location, schedule.BookingDeadline,
            windows.Select(w => new ScheduleWindowInput(w.DayOfWeek, ScheduleRules.Format(w.StartTime), ScheduleRules.Format(w.EndTime))).ToList(),
            slots.Select(s =>
            {
                var booking = bookings.GetValueOrDefault(s.Id);
                return Slot(s, booking is null ? SlotStates.Free : SlotStates.Taken, booking?.WorkspaceId, booking is null ? null : names.GetValueOrDefault(booking.WorkspaceId));
            }).ToList(),
            teams.Count,
            teams.Where(t => !booked.Contains(t.Id)).Select(t => new TeamWithoutSlot(t.Id, t.TeamName)).ToList(),
            slots.Count < teams.Count ? [ScheduleWarnings.NotEnoughSlots] : []);
    }

    private async Task<TeamMeetingScheduleResponse> ToTeamResponseAsync(MeetingSchedule schedule, Guid workspaceId, WorkspaceAccess viewer, CancellationToken ct)
    {
        var slots = await schedules.ListSlotsAsync(schedule.Id, ct);
        var bookings = (await schedules.ListBookingsAsync(schedule.Id, ct)).ToDictionary(b => b.SlotId);
        var views = slots.Select(s => Slot(s, !bookings.TryGetValue(s.Id, out var booking)
            ? SlotStates.Free
            : booking.WorkspaceId == workspaceId ? SlotStates.Mine : SlotStates.Taken, null, null)).ToList();
        return new TeamMeetingScheduleResponse(
            schedule.LecturerName, schedule.Status, schedule.Location, schedule.BookingDeadline, viewer.IsLeader && CanBook(schedule),
            views.FirstOrDefault(s => s.State == SlotStates.Mine), views);
    }

    private static ScheduleSlotResponse Slot(ScheduleSlot slot, string state, Guid? workspaceId, string? teamName) =>
        new(slot.Id, slot.DayOfWeek, ScheduleRules.Format(slot.StartTime), ScheduleRules.Format(slot.EndTime), state, workspaceId, teamName);
}
