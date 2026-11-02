using System.Globalization;
using BuildingBlocks.Errors;
using FluentValidation;

namespace Collab.Services.Meetings;

// Schema của collab.yaml (tag MeetingSchedules). JSON camelCase, thời gian UTC; thứ và giờ trong tuần là giờ Việt Nam.

/// <summary>Schema <c>ScheduleWindow</c>: <see cref="DayOfWeek"/> 1 = thứ Hai … 7 = Chủ nhật, giờ dạng <c>HH:mm</c>.</summary>
public sealed record ScheduleWindowInput(int DayOfWeek, string? StartTime, string? EndTime);

/// <summary>Schema <c>MeetingScheduleInput</c>. <see cref="SlotMinutes"/> bỏ trống: giữ độ dài đang có, lịch mới theo cài đặt học kỳ.</summary>
public sealed record MeetingScheduleInput(IReadOnlyList<ScheduleWindowInput>? Windows, int? SlotMinutes, string? Location);

/// <summary>Body của <c>PUT /api/workspaces/{id}/slot-booking</c> và <c>PUT /api/meeting-schedules/me/bookings/{workspaceId}</c>.</summary>
public sealed record SlotBookingRequest(Guid SlotId);

/// <summary>Schema <c>ScheduleSlot</c>. <see cref="WorkspaceId"/>, <see cref="TeamName"/> chỉ trả cho giảng viên.</summary>
public sealed record ScheduleSlotResponse(Guid Id, int DayOfWeek, string StartTime, string EndTime, string State, Guid? WorkspaceId, string? TeamName);

/// <summary>Nhóm chưa có slot trong schema <c>MeetingSchedule</c>.</summary>
public sealed record TeamWithoutSlot(Guid WorkspaceId, string TeamName);

/// <summary>Schema <c>MeetingSchedule</c> – góc nhìn giảng viên.</summary>
public sealed record MeetingScheduleResponse(
    Guid Id, string Status, int SlotMinutes, string Location, DateTimeOffset BookingDeadline, IReadOnlyList<ScheduleWindowInput> Windows,
    IReadOnlyList<ScheduleSlotResponse> Slots, int TeamCount, IReadOnlyList<TeamWithoutSlot> TeamsWithoutSlot, IReadOnlyList<string> Warnings);

/// <summary>Schema <c>TeamMeetingSchedule</c> – góc nhìn nhóm, không lộ tên nhóm khác.</summary>
public sealed record TeamMeetingScheduleResponse(
    string LecturerName, string Status, string Location, DateTimeOffset BookingDeadline, bool CanBook, ScheduleSlotResponse? MySlot,
    IReadOnlyList<ScheduleSlotResponse> Slots);

/// <summary>Kết quả <c>POST /internal/meeting-schedules/close-booking</c>.</summary>
public sealed record CloseBookingResponse(int AutoBooked, int TeamsWithoutSlot, int MeetingsCreated);

/// <summary>Body của <c>POST /api/meetings/{id}/reschedule</c>.</summary>
public sealed record RescheduleMeetingRequest(DateTimeOffset? StartsAt, string? Reason);

public static class SlotStates
{
    public const string Free = "FREE";
    public const string Taken = "TAKEN";
    public const string Mine = "MINE";
}

public static class ScheduleWarnings
{
    public const string NotEnoughSlots = "NOT_ENOUGH_SLOTS";
}

// Payload sự kiện exe.collab (docs/contracts/events.md).

public sealed record MeetingSlotsPublishedEvent(
    Guid ScheduleId, Guid LecturerId, string LecturerName, DateTimeOffset BookingDeadline, int SlotCount, IReadOnlyList<MeetingInvitee> Recipients);

public sealed record MeetingSlotBookedEvent(
    Guid WorkspaceId, Guid ScheduleId, Guid SlotId, int DayOfWeek, string StartTime, string EndTime, string Source, IReadOnlyList<MeetingInvitee> Recipients);

public sealed class MeetingScheduleInputValidator : AbstractValidator<MeetingScheduleInput>
{
    public MeetingScheduleInputValidator()
    {
        RuleFor(x => x.Windows).NotEmpty().WithMessage("Thêm ít nhất một khung giờ.");
        RuleFor(x => x.SlotMinutes).InclusiveBetween(ScheduleRules.MinSlotMinutes, ScheduleRules.MaxSlotMinutes)
            .WithMessage($"Độ dài slot từ {ScheduleRules.MinSlotMinutes} đến {ScheduleRules.MaxSlotMinutes} phút.");
        RuleFor(x => x.Location).NotEmpty().WithMessage("Nhập link họp.").MaximumLength(500).WithMessage("Link họp tối đa 500 ký tự.");
        RuleForEach(x => x.Windows).ChildRules(window =>
        {
            window.RuleFor(w => w.DayOfWeek).InclusiveBetween(1, 7).WithMessage("Thứ từ 1 (thứ Hai) đến 7 (Chủ nhật).");
            window.RuleFor(w => w.StartTime).Must(t => ScheduleRules.TryParseTime(t, out _)).WithMessage("Giờ bắt đầu dạng HH:mm.");
            window.RuleFor(w => w.EndTime).Must(t => ScheduleRules.TryParseTime(t, out _)).WithMessage("Giờ kết thúc dạng HH:mm.");
        });
    }
}

public sealed class SlotBookingRequestValidator : AbstractValidator<SlotBookingRequest>
{
    public SlotBookingRequestValidator()
    {
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("Chọn slot.");
    }
}

public sealed class RescheduleMeetingRequestValidator : AbstractValidator<RescheduleMeetingRequest>
{
    public RescheduleMeetingRequestValidator()
    {
        RuleFor(x => x.StartsAt).NotNull().WithMessage("Chọn giờ mới.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do dời.").MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}

/// <summary>Một slot sau khi cắt khung giờ.</summary>
public sealed record CutSlot(short DayOfWeek, TimeOnly Start, TimeOnly End);

/// <summary>Một buổi họp hằng tuần.</summary>
public sealed record WeeklyOccurrence(short WeekNo, DateTimeOffset StartsAt);

/// <summary>Quy tắc lịch họp hằng tuần (rule 25), không cần DB.</summary>
public static class ScheduleRules
{
    public const int MinSlotMinutes = 5;
    public const int MaxSlotMinutes = 60;
    public const int DefaultSlotMinutes = 15;

    /// <summary>Hạn chọn slot mặc định (số ngày từ lúc tạo lịch) khi chưa đọc được cài đặt học kỳ.</summary>
    public const int FallbackDeadlineDays = 14;

    /// <summary><c>ck_meetings_weekly</c>.</summary>
    public const int MaxWeekNo = 20;

    /// <summary><c>ck_meeting_slots_duration</c> yêu cầu ít nhất 15 phút, trong khi slot được phép ngắn tới 5 phút.</summary>
    public const int MinMeetingMinutes = 15;

    /// <summary>Thứ và giờ trong lịch là giờ Việt Nam (không có giờ mùa hè).</summary>
    public static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7);

    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>1 = thứ Hai … 7 = Chủ nhật.</summary>
    public static int IsoDay(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    /// <summary>
    /// Khung giờ sau khi kiểm tra: giờ kết thúc sau giờ bắt đầu, đủ chỗ cho một slot, không chồng nhau trong cùng ngày.
    /// Trả danh sách đã sắp; sai thì ném 400.
    /// </summary>
    public static IReadOnlyList<(short Day, TimeOnly Start, TimeOnly End)> ParseWindows(IReadOnlyList<ScheduleWindowInput> windows, int slotMinutes)
    {
        var parsed = windows.Select(w =>
        {
            TryParseTime(w.StartTime, out var start);
            TryParseTime(w.EndTime, out var end);
            return (Day: (short)w.DayOfWeek, Start: start, End: end);
        }).OrderBy(w => w.Day).ThenBy(w => w.Start).ToList();

        foreach (var window in parsed)
        {
            if (window.End <= window.Start || (window.End - window.Start).TotalMinutes < slotMinutes)
            {
                throw MeetingErrors.Invalid("windows", $"Mỗi khung giờ phải dài ít nhất {slotMinutes} phút.");
            }
        }
        for (var i = 1; i < parsed.Count; i++)
        {
            if (parsed[i].Day == parsed[i - 1].Day && parsed[i].Start < parsed[i - 1].End)
            {
                throw MeetingErrors.Invalid("windows", "Các khung giờ trong cùng một ngày không được chồng nhau.");
            }
        }
        return parsed;
    }

    /// <summary>Cắt khung giờ thành slot liền nhau; phần dư cuối khung không đủ một slot thì bỏ.</summary>
    public static IReadOnlyList<CutSlot> Cut(short day, TimeOnly start, TimeOnly end, int slotMinutes)
    {
        var slots = new List<CutSlot>();
        var minutes = (int)(end - start).TotalMinutes;
        for (var offset = 0; offset + slotMinutes <= minutes; offset += slotMinutes)
        {
            slots.Add(new CutSlot(day, start.AddMinutes(offset), start.AddMinutes(offset + slotMinutes)));
        }
        return slots;
    }

    /// <summary>Hạn chọn slot: 23:59:59 ngày cuối của tuần <paramref name="deadlineWeek"/> (tính từ ngày bắt đầu học kỳ), giờ Việt Nam.</summary>
    public static DateTimeOffset BookingDeadline(DateOnly semesterStart, int deadlineWeek) =>
        Local(semesterStart.AddDays(7 * deadlineWeek - 1), new TimeOnly(23, 59, 59));

    public static DateTimeOffset Local(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), LocalOffset).ToUniversalTime();

    /// <summary>Tuần học chứa <paramref name="date"/>, tuần đầu là 1.</summary>
    public static int WeekNo(DateOnly semesterStart, DateOnly date) => (date.DayNumber - semesterStart.DayNumber) / 7 + 1;

    /// <summary>Các buổi họp của slot từ sau <paramref name="after"/> tới hết học kỳ, mỗi tuần một buổi.</summary>
    public static IReadOnlyList<WeeklyOccurrence> Occurrences(DateOnly semesterStart, DateOnly semesterEnd, int dayOfWeek, TimeOnly time, DateTimeOffset after)
    {
        var occurrences = new List<WeeklyOccurrence>();
        var date = semesterStart.AddDays((dayOfWeek - IsoDay(semesterStart) + 7) % 7);
        for (; date <= semesterEnd; date = date.AddDays(7))
        {
            var startsAt = Local(date, time);
            var week = WeekNo(semesterStart, date);
            if (startsAt > after && week <= MaxWeekNo)
            {
                occurrences.Add(new WeeklyOccurrence((short)week, startsAt));
            }
        }
        return occurrences;
    }

    /// <summary>Giờ của slot trong đúng tuần học <paramref name="weekNo"/>.</summary>
    public static DateTimeOffset InWeek(DateOnly semesterStart, int weekNo, int dayOfWeek, TimeOnly time)
    {
        var first = semesterStart.AddDays((dayOfWeek - IsoDay(semesterStart) + 7) % 7);
        return Local(first.AddDays(7 * (weekNo - 1)), time);
    }

    /// <summary>Thời lượng họp theo slot, không dưới <see cref="MinMeetingMinutes"/>.</summary>
    public static short MeetingMinutes(int slotMinutes) => (short)Math.Max(slotMinutes, MinMeetingMinutes);
}

/// <summary>Lỗi của lịch họp hằng tuần (collab.yaml tag MeetingSchedules).</summary>
public static class MeetingScheduleErrors
{
    public static ConflictException SlotTaken() => new("slot-taken", "Slot vừa có nhóm khác chọn", "Hãy chọn một slot còn trống.");

    public static ConflictException SlotBooked() =>
        new("slot-booked", "Khung giờ mới bỏ slot đã có nhóm", "Đổi nhóm sang slot khác trước khi bỏ slot này.");

    public static ConflictException BookingClosed() =>
        new("booking-closed", "Đã hết hạn chọn slot", "Giảng viên vẫn đổi được slot cho nhóm.");

    public static ConflictException NotLeader() => new("not-leader", "Chỉ trưởng nhóm chọn được slot");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException NoTeams() =>
        new("no-exe201-team", "Bạn chưa hướng dẫn nhóm EXE201 nào", "Lịch họp hằng tuần tạo được sau khi có nhóm EXE201.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException NoSlots() => new("schedule-has-no-slot", "Lịch chưa có slot", "Thêm khung giờ trước khi đăng.");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung.
    public static ConflictException NotWeekly() => new("meeting-not-weekly", "Chỉ dời được họp hằng tuần");

    // Mã chưa có trong docs/contracts/README.md – đề xuất bổ sung. Job gặp lỗi này sẽ thử lại.
    public static HttpStatusException SemesterUnavailable(Guid semesterId) =>
        new(503, "semester-unavailable", "Không đọc được học kỳ", $"Chưa đọc được lịch học kỳ {semesterId} từ Academic.");
}
