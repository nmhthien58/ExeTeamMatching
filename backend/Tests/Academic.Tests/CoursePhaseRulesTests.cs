using Academic.Services.Rounds;
using Academic.Services.Semesters;

namespace Academic.Tests;

/// <summary>Giai đoạn môn (BE-67, rule 18), không cần DB.</summary>
public sealed class CoursePhaseRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 3, 0, 0, TimeSpan.Zero);

    private static readonly SemesterResponse Semester =
        new(Guid.NewGuid(), "FA26", "Fall 2026", new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 26), true, 6, 3, 2, 15, null);

    [Fact]
    public void Before_any_round_course_is_in_team_formation()
    {
        var phase = CoursePhaseRules.Compute("EXE101", new PhaseSource(Semester, []), Now);
        Assert.Equal(("TEAM_FORMATION", (DateTimeOffset?)null, false), (phase.Phase, phase.TeamFormationDeadlineAt, phase.ChangeRequestRoundOpen));
        Assert.Equal(new DateOnly(2026, 10, 12), phase.MentoringStartsOn); // đầu tuần 6
    }

    [Fact]
    public void Open_round_keeps_team_formation_until_its_deadline()
    {
        var round = Round("EXE101", "TEAM_FORMATION", "OPEN", Now.AddDays(-3), Now.AddHours(5));
        var phase = CoursePhaseRules.Compute("EXE101", new PhaseSource(Semester, [round]), Now);
        Assert.Equal(("TEAM_FORMATION", (DateTimeOffset?)round.DeadlineAt), (phase.Phase, phase.TeamFormationDeadlineAt));

        Assert.Equal("SEMESTER_RUNNING", CoursePhaseRules.Compute("EXE101", new PhaseSource(Semester, [round]), Now.AddHours(6)).Phase); // quá hạn, Job chưa khóa
        Assert.Equal("TEAM_FORMATION", CoursePhaseRules.Compute("EXE201", new PhaseSource(Semester, [round]), Now).Phase); // môn khác chưa có đợt
    }

    [Fact]
    public void Closed_round_means_semester_running_unless_a_new_round_opens()
    {
        var closed = Round("EXE201", "TEAM_FORMATION", "CLOSED", Now.AddDays(-20), Now.AddDays(-10));
        Assert.Equal("SEMESTER_RUNNING", CoursePhaseRules.Compute("EXE201", new PhaseSource(Semester, [closed]), Now).Phase);

        var reopened = Round("EXE201", "TEAM_FORMATION", "OPEN", Now.AddDays(-1), Now.AddDays(2));
        var phase = CoursePhaseRules.Compute("EXE201", new PhaseSource(Semester, [closed, reopened]), Now);
        Assert.Equal(("TEAM_FORMATION", (DateTimeOffset?)reopened.DeadlineAt), (phase.Phase, phase.TeamFormationDeadlineAt));
    }

    [Fact]
    public void Change_request_round_is_reported_for_exe201_only()
    {
        var change = Round("EXE201", "CHANGE_REQUEST", "OPEN", Now.AddDays(-1), Now.AddDays(5));
        Assert.True(CoursePhaseRules.Compute("EXE201", new PhaseSource(Semester, [change]), Now).ChangeRequestRoundOpen);
        Assert.False(CoursePhaseRules.Compute("EXE101", new PhaseSource(Semester, [change]), Now).ChangeRequestRoundOpen);
        Assert.False(CoursePhaseRules.Compute("EXE201", new PhaseSource(Semester, [change with { Status = "CLOSED" }]), Now).ChangeRequestRoundOpen);
    }

    private static RoundResponse Round(string course, string type, string status, DateTimeOffset opens, DateTimeOffset deadline) =>
        new(Guid.NewGuid(), Semester.Id, course, type, "Đợt", opens, deadline, status, status == "CLOSED" ? deadline : null);
}
