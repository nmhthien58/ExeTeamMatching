using BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Context;
using Team.Repositories.JoinRequests;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;

namespace Team.Tests;

/// <summary>
/// Chạy với team_db thật (DB script + trigger). Bật bằng <c>EXE_INTEGRATION=1</c> sau
/// <c>docker compose -f deploy/docker-compose.yml up -d postgres</c>; chuỗi kết nối đổi được qua <c>EXE_TEAM_DB</c>.
/// Mỗi test dùng học kỳ ngẫu nhiên và xóa nhóm đã tạo.
/// </summary>
public sealed class TeamDbIntegrationTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly Guid _semesterId = Guid.NewGuid();

    private static TeamDbContext NewDb() => new(new DbContextOptionsBuilder<TeamDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("EXE_TEAM_DB")
            ?? "Host=localhost;Port=5432;Database=team_db;Username=team_svc;Password=team_dev_pw")
        .UseSnakeCaseNamingConvention().Options);

    [Fact]
    public async Task Team_with_leader_saves_and_trigger_counts_members()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm đếm", members: 1);

        await using var db = NewDb();
        var saved = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        Assert.Equal(1, saved.MemberCount);
        Assert.NotEqual(0u, saved.Version);
        Assert.NotEqual(default, saved.CreatedAt);
    }

    [Fact]
    public async Task Adding_member_and_editing_team_in_one_save_does_not_conflict()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm sửa cùng lúc", members: 1);

        await using var db = NewDb();
        var loaded = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        loaded.Name = "Nhóm đã đổi tên";
        db.Add(Member(loaded, "SE000002", "MEMBER", "INVITATION"));
        await db.SaveChangesAsync();

        await using var check = NewDb();
        var saved = await check.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        Assert.Equal(("Nhóm đã đổi tên", (short)2), (saved.Name, saved.MemberCount));
    }

    [Fact]
    public async Task Removing_member_and_editing_team_in_one_save_does_not_conflict()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm có người rời", members: 2);

        await using var db = NewDb();
        var loaded = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        loaded.Name = "Nhóm còn một người";
        db.Remove(await db.Set<TeamMember>().SingleAsync(m => m.TeamId == team.Id && m.Role == "MEMBER"));
        await db.SaveChangesAsync();

        await using var check = NewDb();
        Assert.Equal(1, (await check.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id)).MemberCount);
    }

    [Fact]
    public async Task Stale_xmin_becomes_concurrency_conflict()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm tranh chấp", members: 1);

        await using var first = NewDb();
        await using var second = NewDb();
        var a = await first.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        var b = await second.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
        a.Name = "Tên của A";
        await first.SaveChangesAsync();

        b.Name = "Tên của B";
        var ex = await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());
        Assert.Equal("concurrency-conflict", ex.Code);
    }

    [Fact]
    public async Task Student_in_two_teams_of_a_semester_is_already_in_team()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm một", members: 1);
        var other = await CreateTeamAsync("Nhóm hai", members: 1);

        await using var db = NewDb();
        var existing = await db.Set<TeamMember>().FirstAsync(m => m.TeamId == team.Id);
        db.Add(new TeamMember
        {
            TeamId = other.Id, SemesterId = _semesterId, StudentId = existing.StudentId, StudentCode = existing.StudentCode,
            FullName = existing.FullName, SpecializationId = 1, JoinSource = "INVITATION",
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        Assert.Equal("already-in-team", ex.Code);
    }

    [Fact]
    public async Task Join_request_without_ideas_fails_at_commit_with_ideas_required()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync("Nhóm nhận yêu cầu", members: 1);

        await using var db = NewDb();
        db.Add(new JoinRequest { TeamId = team.Id, StudentId = Guid.NewGuid(), HasNoIdea = false });

        var ex = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        Assert.Equal("ideas-required", ex.Code);
    }

    [Fact]
    public async Task Inherited_exe201_team_saves_project_change_request_and_advisor()
    {
        if (!Enabled)
        {
            return;
        }

        await using var db = NewDb();
        var project = new Project { Id = Guid.NewGuid(), Name = "Đề tài thử", OriginSemesterId = _semesterId, OriginCourseCode = "EXE101" };
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = _semesterId, CourseCode = "EXE201", Name = "Nhóm kế thừa", ProjectId = project.Id, IsInherited = true };
        var request = new ChangeRequest { Id = Guid.NewGuid(), TeamId = team.Id, RoundId = Guid.NewGuid(), RequestedByStudentId = Guid.NewGuid(), Reason = "Bổ sung một thành viên mới", ChangedMemberCount = 1, ChangedPercent = 20m };
        var run = new AdvisorMatchingRun { Id = Guid.NewGuid(), AdvisorKind = "LECTURER", SemesterId = _semesterId, CourseCode = "EXE201", Trigger = "SCHEDULED" };
        db.AddRange(project, team, Member(team, "SE000001", "LEADER", "INHERITED"), request, run);
        db.Add(new ProjectVersion { ProjectId = project.Id, Version = 1, Name = project.Name, ChangeType = "CREATED", SemesterId = _semesterId, CourseCode = "EXE101" });
        db.Add(new ChangeRequestItem { TeamId = team.Id, ChangeRequestId = request.Id, Action = "ADD", Source = "INVITATION", StudentId = Guid.NewGuid(), StudentCode = "SE000009", FullName = "Thành viên mới", SpecializationId = 2 });
        db.Add(new LecturerPreference { TeamId = team.Id, Rank = 1, LecturerId = Guid.NewGuid(), LecturerName = "Giảng viên A", UpdatedByStudentId = Guid.NewGuid() });
        db.Add(new LecturerAssignment { TeamId = team.Id, LecturerId = Guid.NewGuid(), LecturerName = "Giảng viên A", Reason = "PREFERENCE", PreferenceRank = 1, MatchingRunId = run.Id });
        await db.SaveChangesAsync();

        await using var check = NewDb();
        var saved = await check.Set<ChangeRequest>().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(("EXE201", "PENDING", 20m), (saved.CourseCode, saved.Status, saved.ChangedPercent));

        // Dọn trước khi xóa nhóm: change_requests và lecturer_assignments chặn xóa nhóm (ON DELETE RESTRICT).
        await check.Set<LecturerAssignment>().Where(a => a.TeamId == team.Id).ExecuteDeleteAsync();
        await check.Set<ChangeRequest>().Where(r => r.TeamId == team.Id).ExecuteDeleteAsync();
        await check.Set<AdvisorMatchingRun>().Where(r => r.Id == run.Id).ExecuteDeleteAsync();
        await check.Set<TeamEntity>().Where(t => t.Id == team.Id).ExecuteDeleteAsync();
        await check.Set<Project>().Where(p => p.Id == project.Id).ExecuteDeleteAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!Enabled)
        {
            return;
        }
        await using var db = NewDb();
        await db.Set<TeamEntity>().Where(t => t.SemesterId == _semesterId).ExecuteDeleteAsync();
    }

    private async Task<TeamEntity> CreateTeamAsync(string name, int members)
    {
        await using var db = NewDb();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = _semesterId, CourseCode = "EXE101", Name = name };
        db.Add(team);
        for (var i = 0; i < members; i++)
        {
            db.Add(Member(team, $"SE{Random.Shared.Next(100000, 999999)}", i == 0 ? "LEADER" : "MEMBER", i == 0 ? "CREATOR" : "INVITATION"));
        }
        await db.SaveChangesAsync();
        return team;
    }

    private TeamMember Member(TeamEntity team, string code, string role, string source) => new()
    {
        TeamId = team.Id, SemesterId = _semesterId, StudentId = Guid.NewGuid(), StudentCode = code,
        FullName = "Sinh viên " + code, SpecializationId = 1, Role = role, JoinSource = source,
    };
}
