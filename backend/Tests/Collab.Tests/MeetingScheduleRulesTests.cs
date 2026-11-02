using BuildingBlocks.Errors;
using Collab.Services.Meetings;

namespace Collab.Tests;

/// <summary>Quy tắc lịch họp hằng tuần (BE-82, rule 25), không cần DB.</summary>
public sealed class MeetingScheduleRulesTests
{
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private static TimeOnly T(string time) => TimeOnly.Parse(time);

    [Fact]
    public void Window_is_cut_into_slots_and_leftover_is_dropped()
    {
        var slots = ScheduleRules.Cut(2, T("09:00"), T("09:40"), 15);
        Assert.Equal([("09:00", "09:15"), ("09:15", "09:30")], slots.Select(s => (ScheduleRules.Format(s.Start), ScheduleRules.Format(s.End))));
        Assert.All(slots, s => Assert.Equal(2, s.DayOfWeek));
    }

    [Fact]
    public void Windows_must_fit_a_slot_and_not_overlap()
    {
        ScheduleWindowInput Window(int day, string start, string end) => new(day, start, end);

        var sorted = ScheduleRules.ParseWindows([Window(3, "13:00", "14:00"), Window(2, "10:00", "11:00"), Window(2, "09:00", "10:00")], 15);
        Assert.Equal([(short)2, (short)2, (short)3], sorted.Select(w => w.Day)); // sát nhau được, sắp theo thứ và giờ
        Assert.Equal(T("09:00"), sorted[0].Start);

        Assert.Throws<RequestValidationException>(() => ScheduleRules.ParseWindows([Window(2, "09:00", "09:10")], 15));
        Assert.Throws<RequestValidationException>(() => ScheduleRules.ParseWindows([Window(2, "11:00", "09:00")], 15));
        Assert.Throws<RequestValidationException>(() => ScheduleRules.ParseWindows([Window(2, "09:00", "10:00"), Window(2, "09:45", "11:00")], 15));
    }

    [Theory]
    [InlineData("09:00", true)]
    [InlineData("23:59", true)]
    [InlineData("9:00", false)]
    [InlineData("24:00", false)]
    [InlineData("sáng", false)]
    [InlineData(null, false)]
    public void Times_are_hh_mm(string? text, bool valid) => Assert.Equal(valid, ScheduleRules.TryParseTime(text, out _));

    [Fact]
    public void Deadline_is_the_last_second_of_the_week_in_vietnam_time()
    {
        var deadline = ScheduleRules.BookingDeadline(Monday, 2); // tuần 2 kết thúc Chủ nhật 20/9
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 23, 59, 59, TimeSpan.FromHours(7)), deadline);
        Assert.Equal(TimeSpan.Zero, deadline.Offset);
    }

    [Theory]
    [InlineData("2026-09-07", 1)] // thứ Hai
    [InlineData("2026-09-13", 7)] // Chủ nhật
    [InlineData("2026-09-09", 3)]
    public void Days_count_from_monday(string date, int iso) => Assert.Equal(iso, ScheduleRules.IsoDay(DateOnly.Parse(date)));

    [Fact]
    public void Occurrences_run_weekly_until_the_end_of_semester()
    {
        var after = ScheduleRules.Local(new DateOnly(2026, 9, 20), new TimeOnly(23, 59)); // hết tuần 2
        var weeks = ScheduleRules.Occurrences(Monday, new DateOnly(2026, 10, 11), 2, T("09:15"), after);

        Assert.Equal([(short)3, (short)4, (short)5], weeks.Select(w => w.WeekNo));
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 9, 15, 0, TimeSpan.FromHours(7)), weeks[0].StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 9, 15, 0, TimeSpan.FromHours(7)), weeks[^1].StartsAt);
    }

    [Fact]
    public void Semester_not_starting_on_monday_keeps_week_numbers_by_start_date()
    {
        var wednesday = new DateOnly(2026, 9, 9);
        var weeks = ScheduleRules.Occurrences(wednesday, new DateOnly(2026, 9, 30), 1, T("08:00"), DateTimeOffset.MinValue);

        Assert.Equal([new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28)],
            weeks.Select(w => DateOnly.FromDateTime(w.StartsAt.ToOffset(ScheduleRules.LocalOffset).DateTime)));
        Assert.Equal([(short)1, (short)2, (short)3], weeks.Select(w => w.WeekNo)); // thứ Hai 14/9 vẫn thuộc tuần học đầu
        Assert.Equal(weeks[1].StartsAt, ScheduleRules.InWeek(wednesday, 2, 1, T("08:00")));
    }

    [Fact]
    public void Occurrences_stop_at_the_week_limit_of_the_database()
    {
        var weeks = ScheduleRules.Occurrences(Monday, Monday.AddDays(7 * 30), 1, T("08:00"), DateTimeOffset.MinValue);
        Assert.Equal(ScheduleRules.MaxWeekNo, weeks.Max(w => w.WeekNo));
    }

    [Theory]
    [InlineData(5, 15)]
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    public void Meeting_length_respects_the_database_minimum(int slotMinutes, int meetingMinutes) =>
        Assert.Equal(meetingMinutes, ScheduleRules.MeetingMinutes(slotMinutes));

    [Fact]
    public void Input_validator_checks_every_field()
    {
        var validator = new MeetingScheduleInputValidator();
        var valid = new MeetingScheduleInput([new ScheduleWindowInput(2, "09:00", "11:00")], 15, "https://meet.example.test/abc");
        Assert.True(validator.Validate(valid).IsValid);
        Assert.True(validator.Validate(valid with { SlotMinutes = null }).IsValid);

        Assert.False(validator.Validate(valid with { Windows = [] }).IsValid);
        Assert.False(validator.Validate(valid with { SlotMinutes = 4 }).IsValid);
        Assert.False(validator.Validate(valid with { SlotMinutes = 61 }).IsValid);
        Assert.False(validator.Validate(valid with { Location = " " }).IsValid);
        Assert.False(validator.Validate(valid with { Windows = [new ScheduleWindowInput(8, "09:00", "11:00")] }).IsValid);
        Assert.False(validator.Validate(valid with { Windows = [new ScheduleWindowInput(2, "9h", "11:00")] }).IsValid);
    }
}
