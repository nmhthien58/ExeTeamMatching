using Academic.Services.Rounds;
using Academic.Services.Semesters;

namespace Academic.Tests;

/// <summary>Kiểm tra dữ liệu học kỳ và đợt đăng ký (BE-16), không cần DB.</summary>
public sealed class SemesterRoundValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 3, 0, 0, TimeSpan.Zero);

    private static SemesterInput Semester(string? code = "SP27", DateOnly? start = null, DateOnly? end = null, IReadOnlyList<int>? checkpoints = null, int? slotMinutes = null) =>
        new(code, "Spring 2027", start ?? new DateOnly(2027, 1, 5), end ?? new DateOnly(2027, 4, 30), null, null, null, null, slotMinutes, checkpoints);

    private static RoundInput Round(string? course = "EXE101", string? type = "TEAM_FORMATION", DateTimeOffset? deadline = null) =>
        new(Guid.NewGuid(), course, type, "Đợt lập nhóm", Now, deadline ?? Now.AddDays(7));

    private static IEnumerable<string> SemesterErrors(SemesterInput input) =>
        new SemesterInputValidator().Validate(input).Errors.Select(e => e.PropertyName).Distinct();

    private static IEnumerable<string> RoundErrors(RoundInput input) =>
        new RoundInputValidator().Validate(input).Errors.Select(e => e.PropertyName).Distinct();

    [Fact]
    public void Valid_semester_passes_with_optional_fields_left_out() => Assert.Empty(SemesterErrors(Semester()));

    [Theory]
    [InlineData("FA2026")]
    [InlineData("fa26")]
    [InlineData("WI26")]
    [InlineData(null)]
    public void Semester_code_must_look_like_FA26(string? code) => Assert.Equal(["Code"], SemesterErrors(Semester(code)));

    [Fact]
    public void Semester_end_must_be_after_start() =>
        Assert.Equal(["EndDate"], SemesterErrors(Semester(start: new DateOnly(2027, 4, 30), end: new DateOnly(2027, 4, 30))));

    [Fact]
    public void Checkpoint_weeks_need_exactly_four_weeks_in_range()
    {
        Assert.Equal(["CheckpointTargetWeeks"], SemesterErrors(Semester(checkpoints: [3, 6, 9])));
        Assert.Equal(["CheckpointTargetWeeks"], SemesterErrors(Semester(checkpoints: [3, 6, 9, 21])));
        Assert.Empty(SemesterErrors(Semester(checkpoints: [3, 6, 9, 12])));
    }

    [Fact]
    public void Meeting_slot_is_5_to_60_minutes() => Assert.Equal(["MeetingSlotMinutes"], SemesterErrors(Semester(slotMinutes: 90)));

    [Fact]
    public void Valid_rounds_pass()
    {
        Assert.Empty(RoundErrors(Round()));
        Assert.Empty(RoundErrors(Round("EXE201", "CHANGE_REQUEST")));
    }

    [Fact]
    public void Change_request_round_is_only_for_EXE201() => Assert.Equal(["RoundType"], RoundErrors(Round("EXE101", "CHANGE_REQUEST")));

    [Fact]
    public void Unknown_course_and_type_are_rejected() => Assert.Equal(["CourseCode", "RoundType"], RoundErrors(Round("EXE301", "OTHER")));

    [Fact]
    public void Deadline_must_be_after_opening() => Assert.Equal(["DeadlineAt"], RoundErrors(Round(deadline: Now)));
}
