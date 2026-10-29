using Matching.Services.Events;
using Matching.Services.ReadModel;

namespace Matching.Tests;

/// <summary>BE-35 – read model dựng từ sự kiện, idempotent và chịu được sự kiện lệch thứ tự.</summary>
public sealed class ReadModelProjectorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Team_events_track_members_and_has_team_flag()
    {
        var s = new Scenario();
        var leader = await s.StudentAsync(Scenario.SE);
        var member = await s.StudentAsync(Scenario.GD);
        var team = await s.TeamAsync("Nhóm A", leader, [member]);

        var view = await s.Model.GetTeamAsync(team, Ct);
        Assert.Equal(new[] { leader, member }.Order(), view!.MemberIds.Order());
        Assert.Equal(team, (await s.Model.GetStudentAsync(member, Ct))!.TeamId);

        await s.Projector.HandleAsync(new MemberLeft(team, member), Ct);

        Assert.DoesNotContain(member, (await s.Model.GetTeamAsync(team, Ct))!.MemberIds);
        Assert.Null((await s.Model.GetStudentAsync(member, Ct))!.TeamId);
    }

    [Fact]
    public async Task Student_imported_into_new_semester_starts_without_team()
    {
        // Id sinh viên dùng chung giữa các học kỳ: nhóm EXE101 không được làm sinh viên "đã có nhóm" ở EXE201.
        var s = new Scenario();
        var student = await s.StudentAsync(Scenario.SE);
        await s.TeamAsync("Nhóm EXE101", student);
        var nextSemester = Guid.NewGuid();

        await s.Projector.HandleAsync(new StudentImported(student, "SV 001", Scenario.SE, nextSemester, Courses.Exe201), Ct);

        Assert.Null((await s.Model.GetStudentAsync(student, Ct))!.TeamId);
        Assert.Contains(student, (await s.Model.GetStudentsAsync(nextSemester, Courses.Exe201, Ct)).Select(x => x.Id));
    }

    [Fact]
    public async Task Student_imported_again_in_same_semester_keeps_team()
    {
        var s = new Scenario();
        var student = await s.StudentAsync(Scenario.SE);
        var team = await s.TeamAsync("Nhóm A", student);

        await s.Projector.HandleAsync(new StudentImported(student, "SV 001", Scenario.SE, s.SemesterId, Courses.Exe101), Ct);

        Assert.Equal(team, (await s.Model.GetStudentAsync(student, Ct))!.TeamId);
    }

    [Fact]
    public async Task Late_member_left_of_old_team_does_not_clear_new_team()
    {
        var s = new Scenario();
        var student = await s.StudentAsync(Scenario.SE);
        var oldTeam = Guid.NewGuid();
        var newTeam = Guid.NewGuid();

        await s.Projector.HandleAsync(new MemberJoined(newTeam, student), Ct);
        await s.Projector.HandleAsync(new MemberLeft(oldTeam, student), Ct);

        Assert.Equal(newTeam, (await s.Model.GetStudentAsync(student, Ct))!.TeamId);
    }

    [Fact]
    public async Task Same_event_twice_gives_same_state()
    {
        var s = new Scenario();
        var leader = await s.StudentAsync(Scenario.SE);
        var team = await s.TeamAsync("Nhóm A", leader);
        var joined = new MemberJoined(team, await s.StudentAsync(Scenario.GD));

        await s.Projector.HandleAsync(joined, Ct);
        await s.Projector.HandleAsync(joined, Ct);

        Assert.Equal(2, (await s.Model.GetTeamAsync(team, Ct))!.MemberIds.Count);
    }

    [Fact]
    public async Task Profile_keeps_only_known_skills_clamped_to_1_5()
    {
        var s = new Scenario();
        var id = await s.StudentAsync(Scenario.SE);

        await s.Projector.HandleAsync(new StudentProfileUpdated(id, null, Scenario.SE,
            [new("TECHNOLOGY", 9), new("UI_UX_DESIGN", 2), new("UNKNOWN", 3)], IsSeekingTeam: true), Ct);

        var student = (await s.Model.GetStudentAsync(id, Ct))!;
        Assert.Equal(5, student.SkillLevel(Skill.Technology));
        Assert.Equal(2, student.SkillLevel(Skill.UiUxDesign));
        Assert.Equal(2, student.Skills.Count);
        Assert.True(student.IsSeekingTeam);
    }

    [Fact]
    public async Task Project_domains_and_exe101_instructor_flow_to_linked_exe201_team()
    {
        var s = new Scenario();
        var lecturer = Guid.NewGuid();
        var exe101Team = await s.TeamAsync("T1", await s.StudentAsync(Scenario.SE));
        var projectId = Guid.NewGuid();
        await s.Projector.HandleAsync(new ProjectCreated(projectId, exe101Team, [4, 10]), Ct);
        await s.Projector.HandleAsync(new LecturerAssigned(exe101Team, lecturer), Ct);

        var exe201Team = await s.TeamAsync("T2", await s.StudentAsync(Scenario.SE, Courses.Exe201), course: Courses.Exe201);
        await s.Projector.HandleAsync(new TeamLinkedToProject(exe201Team, projectId), Ct);

        var view = (await s.Model.GetTeamAsync(exe201Team, Ct))!;
        Assert.Equal([4, 10], view.DomainIds);
        Assert.Equal(lecturer, view.ProjectExe101LecturerId);
        Assert.Null(view.LecturerId);
    }

    [Fact]
    public async Task Interests_and_mentor_follow_assignment_events()
    {
        var s = new Scenario();
        var team = await s.TeamAsync("T", await s.StudentAsync(Scenario.SE));
        var mentor = Guid.NewGuid();
        var lecturer = Guid.NewGuid();

        await s.Projector.HandleAsync(new TopicInterestOpened(team, "LECTURER", lecturer), Ct);
        await s.Projector.HandleAsync(new TopicInterestOpened(team, "MENTOR", mentor), Ct);
        await s.Projector.HandleAsync(new TopicInterestClosed(team, "LECTURER", lecturer), Ct);
        await s.Projector.HandleAsync(new MentorAssigned(team, mentor), Ct);
        await s.Projector.HandleAsync(new MentorUnassigned(team, Guid.NewGuid()), Ct); // mentor khác – bỏ qua

        var view = (await s.Model.GetTeamAsync(team, Ct))!;
        Assert.Empty(view.InterestedLecturerIds);
        Assert.Equal([mentor], view.InterestedMentorIds);
        Assert.Equal(mentor, view.MentorId);
    }

    [Fact]
    public async Task Assignment_before_team_created_is_retried()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<ReadModelNotReadyException>(() =>
            s.Projector.HandleAsync(new LecturerAssigned(Guid.NewGuid(), Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Lecturer_profile_without_domains_keeps_previous_domains()
    {
        var s = new Scenario();
        var lecturer = Guid.NewGuid();
        await s.LecturerAsync(lecturer, 4, 7);

        await s.Projector.HandleAsync(new LecturerProfileUpdated(lecturer, null, IsActive: false), Ct);

        var view = (await s.Model.GetAdvisorAsync(AdvisorKind.Lecturer, lecturer, Ct))!;
        Assert.Equal([4, 7], view.DomainIds);
        Assert.False(view.IsActive);
    }
}
