using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.Members;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.AdvisorAssignments;
using Team.Services.Teams;

namespace Team.Tests;

public sealed class AdvisorAssignmentTests
{
    private sealed class FakeLecturerRepo : ILecturerAssignmentRepository
    {
        public Dictionary<Guid, LecturerAssignment> ByTeam { get; } = [];
        public List<LecturerAssignment> All { get; } = [];

        public Task<LecturerAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(ByTeam.GetValueOrDefault(teamId));

        public Task<List<LecturerAssignment>> ListActiveByLecturerIdAsync(Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(All.Where(a => a.LecturerId == lecturerId && a.Status == "ASSIGNED").ToList());
    }

    private sealed class FakeMentorRepo : IMentorAssignmentRepository
    {
        public Dictionary<Guid, MentorAssignment> ByTeam { get; } = [];
        public List<MentorAssignment> All { get; } = [];

        public Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(ByTeam.GetValueOrDefault(teamId));

        public Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct) =>
            Task.FromResult(All.Where(a => a.MentorId == mentorId && a.Status == "ASSIGNED").ToList());
    }

    private sealed class FakeTeamRepo : ITeamRepository
    {
        public Dictionary<Guid, TeamGraph> Graphs { get; } = [];

        public Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Graphs.GetValueOrDefault(teamId)?.Team);

        public Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Graphs.GetValueOrDefault(teamId));

        public Task<List<TeamGraph>> ListGraphsAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct) =>
            Task.FromResult(teamIds.Where(Graphs.ContainsKey).Select(id => Graphs[id]).ToList());

        public Task<bool> IsStudentInTeamAsync(Guid semesterId, Guid studentId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> IsNameTakenAsync(Guid semesterId, string courseCode, string name, Guid? exceptTeamId, CancellationToken ct) => Task.FromResult(false);
        public Task<Guid?> FindTeamIdOfStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<TeamDetailExtras> GetDetailExtrasAsync(Guid teamId, Guid? viewerStudentId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(new TeamDetailExtras(null, [], false, false, false));
        public Task<TeamHomeStats> GetHomeStatsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(new TeamHomeStats(0, 0, false, false, false, false));
        public Task<int> CountPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task<(List<Guid> Ids, int TotalCount)> SearchAsync(TeamSearch s, CancellationToken ct) => Task.FromResult<(List<Guid>, int)>(([], 0));
        public Task<List<TeamDomain>> ListDomainsAsync(Guid teamId, CancellationToken ct) => Task.FromResult(new List<TeamDomain>());
        public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct) => Task.FromResult<Project?>(null);
        public void Add(object entity) { }
        public void Remove(object entity) { }
        public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload) { }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static ClaimsPrincipal UserWith(Guid? studentId = null, Guid? lecturerId = null, Guid? mentorId = null, params string[] roles)
    {
        var claims = new List<Claim>();
        if (studentId is not null) claims.Add(new Claim(ClaimNames.StudentId, studentId.Value.ToString()));
        if (lecturerId is not null) claims.Add(new Claim(ClaimNames.LecturerId, lecturerId.Value.ToString()));
        if (mentorId is not null) claims.Add(new Claim(ClaimNames.MentorId, mentorId.Value.ToString()));
        foreach (var r in roles) claims.Add(new Claim(ClaimNames.Roles, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static TeamGraph CreateGraph(Guid teamId, string teamName, string courseCode, Guid studentId, string? projectName = null)
    {
        var team = new TeamEntity { Id = teamId, Name = teamName, CourseCode = courseCode, SemesterId = Guid.NewGuid() };
        var members = new List<TeamMember>
        {
            new() { TeamId = teamId, StudentId = studentId, StudentCode = "SE160001", FullName = "Sinh viên A", Role = "LEADER" }
        };
        var project = projectName is null ? null : new Project { Id = Guid.NewGuid(), Name = projectName };
        return new TeamGraph(team, members, [], project, null, null);
    }

    [Fact]
    public async Task GetTeamLecturer_returns_assignment_when_assigned_and_user_is_member()
    {
        var teamId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE101", studentId);

        var lecturerRepo = new FakeLecturerRepo();
        lecturerRepo.ByTeam[teamId] = new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            LecturerId = lecturerId,
            LecturerName = "TS. Nguyễn Văn A",
            Status = "ASSIGNED",
            Reason = "MUTUAL",
            PreferenceRank = 1,
            Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var mentorRepo = new FakeMentorRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var user = UserWith(studentId: studentId, roles: Roles.Student);
        var result = await service.GetTeamLecturerAsync(teamId, user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("TS. Nguyễn Văn A", result.LecturerName);
        Assert.Equal("MUTUAL", result.Reason);
        Assert.Equal((short)1, result.PreferenceRank);
    }

    [Fact]
    public async Task GetTeamLecturer_returns_null_when_not_assigned()
    {
        var teamId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE101", studentId);

        var lecturerRepo = new FakeLecturerRepo();
        var mentorRepo = new FakeMentorRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var user = UserWith(studentId: studentId, roles: Roles.Student);
        var result = await service.GetTeamLecturerAsync(teamId, user, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTeamLecturer_forbidden_for_other_students()
    {
        var teamId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE101", memberId);

        var lecturerRepo = new FakeLecturerRepo();
        var mentorRepo = new FakeMentorRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var user = UserWith(studentId: strangerId, roles: Roles.Student);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetTeamLecturerAsync(teamId, user, CancellationToken.None));
    }

    [Fact]
    public async Task GetTeamLecturer_allowed_for_assigned_mentor()
    {
        var teamId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE101", Guid.NewGuid());

        var lecturerRepo = new FakeLecturerRepo();
        lecturerRepo.ByTeam[teamId] = new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            LecturerId = lecturerId,
            LecturerName = "TS. Nguyễn Văn A",
            Status = "ASSIGNED",
            Reason = "MUTUAL",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var mentorRepo = new FakeMentorRepo();
        mentorRepo.ByTeam[teamId] = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Anh Trần Mentor",
            Status = "ASSIGNED",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var reader = new TeamReader(teamRepo, new FakeAcademic());
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        // Caller is assigned mentor
        var user = UserWith(mentorId: mentorId, roles: Roles.Mentor);
        var result = await service.GetTeamLecturerAsync(teamId, user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("TS. Nguyễn Văn A", result.LecturerName);
    }

    [Fact]
    public async Task GetTeamMentor_returns_assignment_when_assigned()
    {
        var teamId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE201", studentId);

        var lecturerRepo = new FakeLecturerRepo();
        var mentorRepo = new FakeMentorRepo();
        mentorRepo.ByTeam[teamId] = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Anh Trần Mentor",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var user = UserWith(mentorId: mentorId, roles: Roles.Mentor);
        var result = await service.GetTeamMentorAsync(teamId, user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Anh Trần Mentor", result.MentorName);
        Assert.Equal("MENTOR_INTEREST", result.Reason);
        Assert.False(result.IsInternal);
        Assert.Null(result.MentorOrganization);
    }

    [Fact]
    public async Task GetTeamMentor_allowed_for_assigned_lecturer()
    {
        var teamId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm A", "EXE201", Guid.NewGuid());

        var lecturerRepo = new FakeLecturerRepo();
        lecturerRepo.ByTeam[teamId] = new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            LecturerId = lecturerId,
            LecturerName = "TS. Nguyễn Văn A",
            Status = "ASSIGNED",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var mentorRepo = new FakeMentorRepo();
        mentorRepo.ByTeam[teamId] = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Anh Trần Mentor",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            AssignedAt = DateTimeOffset.UtcNow
        };

        var reader = new TeamReader(teamRepo, new FakeAcademic());
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        // Caller is assigned lecturer
        var user = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);
        var result = await service.GetTeamMentorAsync(teamId, user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Anh Trần Mentor", result.MentorName);
        Assert.False(result.IsInternal);
        Assert.Null(result.MentorOrganization);
    }

    [Fact]
    public async Task GetLecturerTeamsMe_filters_by_course_and_search()
    {
        var lecturerId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();
        var team3Id = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[team1Id] = CreateGraph(team1Id, "Nhóm Alpha", "EXE101", Guid.NewGuid(), "Smart Edu");
        teamRepo.Graphs[team2Id] = CreateGraph(team2Id, "Nhóm Beta", "EXE201", Guid.NewGuid(), "Health Care");
        teamRepo.Graphs[team3Id] = CreateGraph(team3Id, "Nhóm Gamma", "EXE101", Guid.NewGuid(), "Agri Tech");

        var lecturerRepo = new FakeLecturerRepo();
        lecturerRepo.All.Add(new LecturerAssignment { TeamId = team1Id, LecturerId = lecturerId, Status = "ASSIGNED", Reason = "MUTUAL", PreferenceRank = 1 });
        lecturerRepo.All.Add(new LecturerAssignment { TeamId = team2Id, LecturerId = lecturerId, Status = "ASSIGNED", Reason = "DOMAIN", PreferenceRank = null });

        var mentorRepo = new FakeMentorRepo();
        mentorRepo.All.Add(new MentorAssignment { TeamId = team3Id, MentorId = mentorId, Status = "ASSIGNED", Reason = "MENTOR_INTEREST" });

        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var user = UserWith(lecturerId: lecturerId, mentorId: mentorId, roles: [Roles.Lecturer, Roles.Mentor]);

        // 1. Without filter
        var res1 = await service.GetLecturerTeamsMeAsync(null, null, null, user, CancellationToken.None);
        Assert.Equal(2, res1.Instructing.Count);
        Assert.Single(res1.Mentoring);

        // 2. Filter courseCode = EXE101
        var res2 = await service.GetLecturerTeamsMeAsync("EXE101", null, null, user, CancellationToken.None);
        Assert.Single(res2.Instructing);
        Assert.Equal("Nhóm Alpha", res2.Instructing[0].Team.Name);
        Assert.Single(res2.Mentoring);

        // 3. Search = "Smart"
        var res3 = await service.GetLecturerTeamsMeAsync(null, null, "Smart", user, CancellationToken.None);
        Assert.Single(res3.Instructing);
        Assert.Equal("Nhóm Alpha", res3.Instructing[0].Team.Name);
        Assert.Empty(res3.Mentoring);
    }

    [Fact]
    public async Task GetLecturerTeamsMe_when_user_has_no_mentor_id_returns_empty_mentoring()
    {
        var lecturerId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        teamRepo.Graphs[teamId] = CreateGraph(teamId, "Nhóm Alpha", "EXE101", Guid.NewGuid());

        var lecturerRepo = new FakeLecturerRepo();
        lecturerRepo.All.Add(new LecturerAssignment { TeamId = teamId, LecturerId = lecturerId, Status = "ASSIGNED", Reason = "MUTUAL" });

        var mentorRepo = new FakeMentorRepo();
        var reader = new TeamReader(teamRepo, new FakeAcademic());
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        // Lecturer has no mentorId claim
        var user = UserWith(lecturerId: lecturerId, mentorId: null, roles: Roles.Lecturer);
        var result = await service.GetLecturerTeamsMeAsync(null, null, null, user, CancellationToken.None);

        Assert.Single(result.Instructing);
        Assert.Empty(result.Mentoring);
        Assert.Null(result.MaxTeams);
    }

    [Fact]
    public async Task GetMentorTeamsMe_when_user_has_no_mentor_id_returns_empty()
    {
        var lecturerId = Guid.NewGuid();

        var teamRepo = new FakeTeamRepo();
        var lecturerRepo = new FakeLecturerRepo();
        var mentorRepo = new FakeMentorRepo();
        var reader = new TeamReader(teamRepo, new FakeAcademic());
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        // User is lecturer without mentorId claim calling mentor-teams/me
        var user = UserWith(lecturerId: lecturerId, mentorId: null, roles: Roles.Lecturer);
        var result = await service.GetMentorTeamsMeAsync(user, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLecturerTeamsMe_requires_lecturer_role()
    {
        var teamRepo = new FakeTeamRepo();
        var lecturerRepo = new FakeLecturerRepo();
        var mentorRepo = new FakeMentorRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader);

        var studentUser = UserWith(studentId: Guid.NewGuid(), roles: Roles.Student);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetLecturerTeamsMeAsync(null, null, null, studentUser, CancellationToken.None));
    }

    [Fact]
    public async Task Student_calling_lecturer_teams_me_api_gets_403_forbidden()
    {
        var api = new TeamApiFactory();
        var client = api.ClientFor(Guid.NewGuid()); // Student role

        var response = await client.GetAsync("/api/lecturer-teams/me");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
