using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AuditLog;
using Team.Repositories.Context;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.Matching;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Tests;

public sealed class TopicInterestsTests
{
    private sealed class InMemoryTopicInterestRepo : ITopicInterestRepository
    {
        public List<TopicInterest> Interests { get; } = [];
        public HashSet<Guid> AssignedLecturerTeamIds { get; } = [];
        public HashSet<Guid> AssignedMentorTeamIds { get; } = [];
        public Dictionary<(string, Guid), int> AssignedCounts { get; } = [];
        public List<(string Kind, Guid AdvisorId, Guid SemesterId, string CourseCode)> Assignments { get; } = [];
        public Dictionary<Guid, (Guid SemesterId, string CourseCode)> TeamScopes { get; } = [];
        public HashSet<(Guid TeamId, Guid LecturerId)> Exe101Instructors { get; } = [];
        public Dictionary<(Guid TeamId, Guid LecturerId), int> PreferenceRanks { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];
        public List<Guid> TeamIdsInSemester { get; } = [];

        public Task<TopicInterest?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Interests.FirstOrDefault(i => i.Id == id));

        public Task<TopicInterest?> GetOpenInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct) =>
            Task.FromResult(Interests.FirstOrDefault(i => i.TeamId == teamId && i.AdvisorKind == advisorKind && i.AdvisorId == advisorId && i.Status == "OPEN"));

        public Task<TopicInterest?> GetLatestInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct) =>
            Task.FromResult(Interests.Where(i => i.TeamId == teamId && i.AdvisorKind == advisorKind && i.AdvisorId == advisorId)
                .OrderByDescending(i => i.CreatedAt).FirstOrDefault());

        public Task<IReadOnlyList<TopicInterest>> ListByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TopicInterest>>(Interests.Where(i => i.AdvisorKind == advisorKind && i.AdvisorId == advisorId).OrderByDescending(i => i.CreatedAt).ToList());

        public Task<IReadOnlyList<TopicInterest>> ListByTeamIdAsync(Guid teamId, string? advisorKind, CancellationToken ct)
        {
            var query = Interests.Where(i => i.TeamId == teamId);
            if (!string.IsNullOrEmpty(advisorKind)) query = query.Where(i => i.AdvisorKind == advisorKind);
            return Task.FromResult<IReadOnlyList<TopicInterest>>(query.OrderBy(i => i.CreatedAt).ToList());
        }

        public Task<IReadOnlyList<TopicInterest>> ListOpenByTeamIdAsync(Guid teamId, string advisorKind, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TopicInterest>>(Interests.Where(i => i.TeamId == teamId && i.AdvisorKind == advisorKind && i.Status == "OPEN").OrderBy(i => i.CreatedAt).ToList());

        public Task<int> CountOpenByAdvisorAsync(string advisorKind, Guid advisorId, Guid semesterId, string courseCode, CancellationToken ct) =>
            Task.FromResult(Interests.Count(i => i.AdvisorKind == advisorKind && i.AdvisorId == advisorId && i.Status == "OPEN"
                && TeamScopes.TryGetValue(i.TeamId, out var scope) && scope.SemesterId == semesterId
                && (advisorKind == AdvisorKinds.Mentor || scope.CourseCode == courseCode)));

        public Task<int> CountOpenByTeamAsync(Guid teamId, string advisorKind, Guid? excludeAdvisorId, CancellationToken ct)
        {
            var query = Interests.Where(i => i.TeamId == teamId && i.AdvisorKind == advisorKind && i.Status == "OPEN");
            if (excludeAdvisorId.HasValue) query = query.Where(i => i.AdvisorId != excludeAdvisorId.Value);
            return Task.FromResult(query.Count());
        }

        public Task<bool> HasActiveAssignmentAsync(Guid teamId, string advisorKind, CancellationToken ct) =>
            Task.FromResult(advisorKind == "LECTURER" ? AssignedLecturerTeamIds.Contains(teamId) : AssignedMentorTeamIds.Contains(teamId));

        public Task<int> CountActiveAssignmentsByAdvisorAsync(string advisorKind, Guid advisorId, Guid semesterId, string courseCode, CancellationToken ct) =>
            Task.FromResult(AssignedCounts.GetValueOrDefault((advisorKind, advisorId), 0) + Assignments.Count(a =>
                a.Kind == advisorKind && a.AdvisorId == advisorId && a.SemesterId == semesterId &&
                (advisorKind == AdvisorKinds.Mentor || a.CourseCode == courseCode)));

        public Task<bool> WasLecturerEXE101InstructorAsync(Guid teamId, Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(Exe101Instructors.Contains((teamId, lecturerId)));

        public Task<int?> GetLecturerPreferenceRankAsync(Guid teamId, Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(PreferenceRanks.TryGetValue((teamId, lecturerId), out var rank) ? (int?)rank : null);

        public Task<IReadOnlyList<Guid>> ListTeamIdsInSemesterAsync(Guid semesterId, string? courseCode, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(TeamIdsInSemester);

        public void Add(TopicInterest interest) => Interests.Add(interest);

        public void AddOutboxMessage<T>(string topic, string eventType, string aggregateType, Guid aggregateId, T payload)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Topic = topic,
                EventType = eventType,
                AggregateType = aggregateType,
                AggregateId = aggregateId,
                Payload = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions),
                OccurredAt = DateTimeOffset.UtcNow,
            });
        }

        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class InMemoryTeamRepo : ITeamRepository
    {
        public List<TeamEntity> Teams { get; } = [];
        public List<TeamMember> Members { get; } = [];
        public List<Project> Projects { get; } = [];
        public List<TeamDomain> Domains { get; } = [];

        public Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Teams.FirstOrDefault(t => t.Id == teamId));

        public Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct)
        {
            var team = Teams.FirstOrDefault(t => t.Id == teamId);
            if (team is null) return Task.FromResult<TeamGraph?>(null);
            var members = Members.Where(m => m.TeamId == teamId).ToList();
            var project = Projects.FirstOrDefault(p => p.Id == team.ProjectId);
            var domains = Domains.Where(d => d.TeamId == teamId).ToList();
            return Task.FromResult<TeamGraph?>(new TeamGraph(team, members, domains, project, null, null));
        }

        public Task<List<TeamGraph>> ListGraphsAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct)
        {
            var res = new List<TeamGraph>();
            foreach (var id in teamIds)
            {
                var team = Teams.FirstOrDefault(t => t.Id == id);
                if (team is not null)
                {
                    var members = Members.Where(m => m.TeamId == id).ToList();
                    var project = Projects.FirstOrDefault(p => p.Id == team.ProjectId);
                    var domains = Domains.Where(d => d.TeamId == id).ToList();
                    res.Add(new TeamGraph(team, members, domains, project, null, null));
                }
            }
            return Task.FromResult(res);
        }

        public Task<(List<Guid> Ids, int TotalCount)> SearchAsync(TeamSearch search, CancellationToken ct) =>
            Task.FromResult((Teams.Select(t => t.Id).ToList(), Teams.Count));

        public Task<Guid?> FindTeamIdOfStudentAsync(Guid studentId, CancellationToken ct) =>
            Task.FromResult(Members.FirstOrDefault(m => m.StudentId == studentId)?.TeamId);

        public Task<bool> IsStudentInTeamAsync(Guid semesterId, Guid studentId, CancellationToken ct) =>
            Task.FromResult(Members.Any(m => m.SemesterId == semesterId && m.StudentId == studentId));

        public Task<bool> IsNameTakenAsync(Guid semesterId, string courseCode, string name, Guid? exceptTeamId, CancellationToken ct) =>
            Task.FromResult(Teams.Any(t => t.SemesterId == semesterId && t.CourseCode == courseCode && t.Name == name && t.Id != exceptTeamId));

        public Task<List<TeamDomain>> ListDomainsAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Domains.Where(d => d.TeamId == teamId).ToList());

        public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == projectId));

        public Task<TeamDetailExtras> GetDetailExtrasAsync(Guid teamId, Guid? viewerStudentId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new TeamDetailExtras(null, [], false, false, false));

        public Task<TeamHomeStats> GetHomeStatsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new TeamHomeStats(0, 0, false, false, false, false));

        public Task<int> CountPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(0);

        public void Add(object entity)
        {
            if (entity is TeamEntity t) Teams.Add(t);
        }

        public void Remove(object entity) { }
        public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload) { }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAcademic : IAcademicClient
    {
        public SemesterDto Semester { get; set; } = new(Guid.NewGuid(), "SP26", "Spring 2026");
        public HashSet<string> OpenCourses { get; } = ["EXE101", "EXE201"];
        public Dictionary<Guid, LecturerSnapshot> Lecturers { get; } = [];
        public Dictionary<Guid, MentorSnapshot> Mentors { get; } = [];

        public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => Task.FromResult<SemesterDto?>(Semester);

        public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
            Task.FromResult(OpenCourses.Contains(courseCode) && roundType == RoundTypes.TeamFormation
                ? new RoundDto(Guid.NewGuid(), Semester.Id, courseCode, roundType, DateTimeOffset.UtcNow.AddDays(7), "OPEN")
                : null);

        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

        public Task<LecturerSnapshot?> GetLecturerAsync(Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(Lecturers.GetValueOrDefault(lecturerId));

        public Task<MentorSnapshot?> GetMentorAsync(Guid mentorId, CancellationToken ct) =>
            Task.FromResult(Mentors.GetValueOrDefault(mentorId));

        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

        public Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>([
                new CatalogItem(1, "D01", "Fintech", true),
                new CatalogItem(2, "D02", "Edtech", true),
                new CatalogItem(3, "D03", "HealthCare", true)
            ]);

        public Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>([
                new CatalogItem(1, "SE", "Software Engineering", true),
                new CatalogItem(2, "IA", "Information Assurance", true)
            ]);
    }

    private sealed class FakeTopicSuggestions : ITopicSuggestionClient
    {
        public List<TopicSuggestionItem> Items { get; } = [];
        public (Guid SemesterId, string? Course, string Kind, Guid AdvisorId, int Page, int PageSize)? Request { get; private set; }
        public Exception? Failure { get; set; }
        public Task<IReadOnlyList<TopicSuggestionItem>> SuggestAsync(Guid semesterId, string? courseCode, string advisorKind, Guid advisorId, int page, int pageSize, CancellationToken ct)
        {
            Request = (semesterId, courseCode, advisorKind, advisorId, page, pageSize);
            if (Failure is not null) throw Failure;
            return Task.FromResult<IReadOnlyList<TopicSuggestionItem>>(Items);
        }
    }

    private sealed class FakeInterestSettings(TopicInterestSettings value) : ITopicInterestSettingsClient
    {
        public Task<TopicInterestSettings> GetAsync(CancellationToken ct) => Task.FromResult(value);
    }

    private static (TopicInterestService Service, InMemoryTopicInterestRepo InterestRepo, InMemoryTeamRepo TeamRepo, FakeAcademic Academic) CreateService(FakeTopicSuggestions? suggestions = null, TopicInterestSettings? settings = null)
    {
        var interestRepo = new InMemoryTopicInterestRepo();
        var teamRepo = new InMemoryTeamRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);
        var audit = new AuditLogWriter(db);
        var service = new TopicInterestService(interestRepo, teamRepo, academic, suggestions ?? new FakeTopicSuggestions(), new FakeInterestSettings(settings ?? new TopicInterestSettings(2, 10)), reader, audit, TimeProvider.System);
        return (service, interestRepo, teamRepo, academic);
    }

    private static ClaimsPrincipal UserWith(Guid? lecturerId = null, Guid? mentorId = null, Guid? studentId = null, params string[] roles)
    {
        var claims = new List<Claim>();
        if (lecturerId.HasValue) claims.Add(new Claim(ClaimNames.LecturerId, lecturerId.Value.ToString()));
        if (mentorId.HasValue) claims.Add(new Claim(ClaimNames.MentorId, mentorId.Value.ToString()));
        if (studentId.HasValue) claims.Add(new Claim(ClaimNames.StudentId, studentId.Value.ToString()));
        claims.Add(new Claim(ClaimNames.Subject, Guid.NewGuid().ToString()));
        claims.Add(new Claim(ClaimNames.Name, "Cố vấn thử nghiệm"));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Recommendations_CountLecturerCapacityForCurrentSemesterAndCourse()
    {
        var (service, interests, _, academic) = CreateService();
        var lecturerId = Guid.NewGuid();
        academic.Lecturers[lecturerId] = new LecturerSnapshot(lecturerId, "GV", "gv@example.test", [], 1, 3, true);
        interests.Assignments.Add(("LECTURER", lecturerId, academic.Semester.Id, "EXE101"));
        interests.Assignments.Add(("LECTURER", lecturerId, academic.Semester.Id, "EXE201"));
        interests.Assignments.Add(("LECTURER", lecturerId, Guid.NewGuid(), "EXE201"));

        var result = await service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 20,
            UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None);

        Assert.Equal(2, result.RemainingCapacity);
        Assert.Equal(4, result.InterestLimit);
    }

    [Fact]
    public async Task Recommendations_DefaultToExe101WhenCourseIsOmitted()
    {
        var suggestions = new FakeTopicSuggestions();
        var (service, _, _, academic) = CreateService(suggestions: suggestions);

        await service.GetRecommendationsAsync("LECTURER", null, 1, 20,
            UserWith(lecturerId: Guid.NewGuid(), roles: Roles.Lecturer), CancellationToken.None);

        Assert.Equal(academic.Semester.Id, suggestions.Request?.SemesterId);
        Assert.Equal("EXE101", suggestions.Request?.Course);
    }

    [Fact]
    public async Task OpenInterest_UsesConfiguredUnlimitedLimit()
    {
        var (service, interests, teams, academic) = CreateService(settings: new TopicInterestSettings(4, 1));
        var lecturerId = Guid.NewGuid();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teams.Teams.Add(team);
        var previousTeamId = Guid.NewGuid();
        interests.TeamScopes[previousTeamId] = (academic.Semester.Id, "EXE101");
        interests.Interests.Add(new TopicInterest { Id = Guid.NewGuid(), TeamId = previousTeamId, AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN" });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("LECTURER"), UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None));
        Assert.Equal("interest-limit", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_DoesNotCountOtherSemestersOrLecturerCourses()
    {
        var (service, interests, teams, academic) = CreateService(settings: new TopicInterestSettings(2, 1));
        var lecturerId = Guid.NewGuid();
        academic.Lecturers[lecturerId] = new LecturerSnapshot(lecturerId, "GV", "gv@example.test", [], null, null, true);
        var target = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm hiện tại", Status = "ACTIVE" };
        teams.Teams.Add(target);
        var oldSemesterTeam = Guid.NewGuid();
        var otherCourseTeam = Guid.NewGuid();
        interests.TeamScopes[oldSemesterTeam] = (Guid.NewGuid(), "EXE101");
        interests.TeamScopes[otherCourseTeam] = (academic.Semester.Id, "EXE201");
        interests.Interests.AddRange([
            new TopicInterest { Id = Guid.NewGuid(), TeamId = oldSemesterTeam, AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN" },
            new TopicInterest { Id = Guid.NewGuid(), TeamId = otherCourseTeam, AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN" },
        ]);

        var opened = await service.OpenInterestAsync(target.Id, new OpenTopicInterestInput("LECTURER"),
            UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None);

        Assert.Equal("OPEN", opened.Status);
    }

    [Fact]
    public async Task OpenInterest_UsesConfiguredExtraSlotsForMentor()
    {
        var (service, interests, teams, academic) = CreateService(settings: new TopicInterestSettings(0, 10));
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), 6);
        var mentorId = Guid.NewGuid();
        academic.Mentors[mentorId] = new MentorSnapshot(mentorId, "Mentor", "mentor@example.test", [], 1, null, true);
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teams.Teams.Add(team);
        var previousTeamId = Guid.NewGuid();
        interests.TeamScopes[previousTeamId] = (academic.Semester.Id, "EXE201");
        interests.Interests.Add(new TopicInterest { Id = Guid.NewGuid(), TeamId = previousTeamId, AdvisorKind = "MENTOR", AdvisorId = mentorId, Status = "OPEN" });
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), UserWith(mentorId: mentorId, roles: Roles.Mentor), CancellationToken.None));
        Assert.Equal("interest-limit", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_ForTeamInPreviousSemester()
    {
        var (service, interestRepo, teamRepo, _) = CreateService();
        var lecturerId = Guid.NewGuid();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = Guid.NewGuid(), CourseCode = "EXE101", Name = "Nhóm kỳ trước", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        await Assert.ThrowsAsync<ConflictException>(() => service.OpenInterestAsync(
            team.Id, new OpenTopicInterestInput("LECTURER"), UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None));
        Assert.Empty(interestRepo.Interests);
        Assert.Empty(interestRepo.OutboxMessages);
    }

    [Fact]
    public async Task InterestLists_OnlyShowOpenInterests()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        var lecturerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);
        teamRepo.Members.Add(new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, StudentId = studentId });
        foreach (var status in new[] { "OPEN", "WITHDRAWN", "CLOSED" })
            interestRepo.Interests.Add(new TopicInterest { Id = Guid.NewGuid(), TeamId = team.Id, AdvisorId = lecturerId, AdvisorKind = "LECTURER", Status = status });

        var mine = await service.GetMyInterestsAsync("LECTURER", UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None);
        var teamInterests = await service.GetTeamInterestsAsync(team.Id, null, UserWith(studentId: studentId, roles: Roles.Student), CancellationToken.None);
        Assert.Equal("OPEN", Assert.Single(mine).Status);
        Assert.Equal("OPEN", Assert.Single(teamInterests).Status);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_User_IsNotAdvisor()
    {
        var (service, _, _, _) = CreateService();
        var studentUser = UserWith(studentId: Guid.NewGuid(), roles: Roles.Student);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.OpenInterestAsync(Guid.NewGuid(), new OpenTopicInterestInput("LECTURER"), studentUser, CancellationToken.None));
        Assert.Contains("giảng viên", ex.Detail);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_RoundClosed_ForLecturer()
    {
        var (service, _, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear(); // Đóng đợt
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("LECTURER"), lecturerUser, CancellationToken.None));
        Assert.Equal("interest-closed", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_TeamAlreadyHasAssignedAdvisor()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);
        interestRepo.AssignedLecturerTeamIds.Add(team.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("LECTURER"), lecturerUser, CancellationToken.None));
        Assert.Equal("team-has-advisor", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_LimitReached_ForLecturer()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        // Giả lập đã đạt giới hạn 10 quan tâm
        for (var i = 0; i < 10; i++)
        {
            var interestedTeamId = Guid.NewGuid();
            interestRepo.TeamScopes[interestedTeamId] = (academic.Semester.Id, "EXE101");
            interestRepo.Interests.Add(new TopicInterest
            {
                Id = Guid.NewGuid(), TeamId = interestedTeamId, AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN"
            });
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("LECTURER"), lecturerUser, CancellationToken.None));
        Assert.Equal("interest-limit", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Succeeds_AndEmits_TopicInterestOpened_With_MemberStudentIds()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE201", Name = "Nhóm Kế Thừa", Status = "ACTIVE" };
        var m1 = new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, StudentId = Guid.NewGuid(), Role = "LEADER", StudentCode = "SE000001", FullName = "Trưởng nhóm" };
        var m2 = new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, StudentId = Guid.NewGuid(), Role = "MEMBER", StudentCode = "SE000002", FullName = "Thành viên" };
        teamRepo.Teams.Add(team);
        teamRepo.Members.AddRange([m1, m2]);
        interestRepo.Exe101Instructors.Add((team.Id, lecturerId));

        var response = await service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("LECTURER"), lecturerUser, CancellationToken.None);

        Assert.Equal("OPEN", response.Status);
        Assert.Equal("LECTURER", response.AdvisorKind);
        Assert.Equal(lecturerId, response.AdvisorId);
        Assert.True(response.WasPreviousInstructor);

        var outbox = Assert.Single(interestRepo.OutboxMessages);
        Assert.Equal("TopicInterestOpened", outbox.EventType);
        Assert.Equal(team.Id, outbox.AggregateId);

        using var doc = JsonDocument.Parse(outbox.Payload);
        var root = doc.RootElement;
        Assert.Equal(response.Id.ToString(), root.GetProperty("interestId").GetString());
        Assert.Equal(team.Id.ToString(), root.GetProperty("teamId").GetString());
        Assert.True(root.GetProperty("wasPreviousInstructor").GetBoolean());

        var memberIds = root.GetProperty("memberStudentIds").EnumerateArray().Select(e => Guid.Parse(e.GetString()!)).ToList();
        Assert.Equal(2, memberIds.Count);
        Assert.Contains(m1.StudentId, memberIds);
        Assert.Contains(m2.StudentId, memberIds);
    }

    [Fact]
    public async Task WithdrawInterest_Succeeds_AndEmits_TopicInterestClosed()
    {
        var (service, interestRepo, _, _) = CreateService();
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var interest = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN"
        };
        interestRepo.Interests.Add(interest);

        await service.WithdrawInterestAsync(interest.Id, lecturerUser, CancellationToken.None);

        Assert.Equal("WITHDRAWN", interest.Status);
        Assert.NotNull(interest.ClosedAt);
        Assert.Null(interest.ClosedReason);

        var outbox = Assert.Single(interestRepo.OutboxMessages);
        Assert.Equal("TopicInterestClosed", outbox.EventType);
        using var doc = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("WITHDRAWN", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task WithdrawInterest_Fails_When_OtherAdvisorAttempts()
    {
        var (service, interestRepo, _, _) = CreateService();
        var ownerId = Guid.NewGuid();
        var otherLecturerUser = UserWith(lecturerId: Guid.NewGuid(), roles: Roles.Lecturer);

        var interest = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), AdvisorKind = "LECTURER", AdvisorId = ownerId, Status = "OPEN"
        };
        interestRepo.Interests.Add(interest);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.WithdrawInterestAsync(interest.Id, otherLecturerUser, CancellationToken.None));
    }

    [Theory]
    [InlineData("LECTURER", "MENTOR")]
    [InlineData("MENTOR", "LECTURER")]
    public async Task CloseInterestsOnAssignment_ClosesAllOpen_AndSetsReasonCorrectly(string advisorKind, string otherKind)
    {
        var (service, interestRepo, _, _) = CreateService();
        var teamId = Guid.NewGuid();
        var matchedAdvisorId = Guid.NewGuid();
        var otherAdvisorId = Guid.NewGuid();

        var interestMatched = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = teamId, AdvisorKind = advisorKind, AdvisorId = matchedAdvisorId, Status = "OPEN"
        };
        var interestOther = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = teamId, AdvisorKind = advisorKind, AdvisorId = otherAdvisorId, Status = "OPEN"
        };
        var otherKindInterest = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = teamId, AdvisorKind = otherKind, AdvisorId = otherAdvisorId, Status = "OPEN"
        };
        var withdrawn = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = teamId, AdvisorKind = advisorKind, AdvisorId = Guid.NewGuid(), Status = "WITHDRAWN"
        };
        interestRepo.Interests.AddRange([otherKindInterest, withdrawn]);
        interestRepo.Interests.AddRange([interestMatched, interestOther]);

        await service.CloseInterestsOnAssignmentAsync(teamId, advisorKind, matchedAdvisorId, CancellationToken.None);
        await service.CloseInterestsOnAssignmentAsync(teamId, advisorKind, matchedAdvisorId, CancellationToken.None);

        Assert.Equal("CLOSED", interestMatched.Status);
        Assert.Equal(ClosedReasons.ThisAdvisorMatched, interestMatched.ClosedReason);

        Assert.Equal("CLOSED", interestOther.Status);
        Assert.Equal(ClosedReasons.OtherAdvisorMatched, interestOther.ClosedReason);
        Assert.Equal("OPEN", otherKindInterest.Status);
        Assert.Equal("WITHDRAWN", withdrawn.Status);

        Assert.Equal(2, interestRepo.OutboxMessages.Count);
        Assert.All(interestRepo.OutboxMessages, m => Assert.Equal("TopicInterestClosed", m.EventType));
        Assert.All(interestRepo.OutboxMessages, m =>
        {
            using var doc = JsonDocument.Parse(m.Payload);
            Assert.Equal(advisorKind, doc.RootElement.GetProperty("advisorKind").GetString());
            Assert.Equal(teamId, doc.RootElement.GetProperty("teamId").GetGuid());
        });
    }

    [Fact]
    public async Task GetTeamInterests_FiltersMentors_ForStudentMembers()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        var studentMember = new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, StudentId = Guid.NewGuid(), Role = "MEMBER", StudentCode = "SE000001", FullName = "Sinh viên" };
        teamRepo.Teams.Add(team);
        teamRepo.Members.Add(studentMember);

        var lecturerInterest = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = team.Id, AdvisorKind = "LECTURER", AdvisorId = Guid.NewGuid(), AdvisorName = "Thầy A", Status = "OPEN"
        };
        var mentorInterest = new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = team.Id, AdvisorKind = "MENTOR", AdvisorId = Guid.NewGuid(), AdvisorName = "Mentor B", Status = "OPEN"
        };
        interestRepo.Interests.AddRange([lecturerInterest, mentorInterest]);

        var memberUser = UserWith(studentId: studentMember.StudentId, roles: Roles.Student);
        var visibleInterests = await service.GetTeamInterestsAsync(team.Id, null, memberUser, CancellationToken.None);

        // Sinh viên chỉ thấy giảng viên, không thấy mentor
        var item = Assert.Single(visibleInterests);
        Assert.Equal("LECTURER", item.AdvisorKind);
        Assert.Equal("Thầy A", item.AdvisorName);
    }

    [Fact]
    public async Task GetTeamInterests_NormalizesKind_AndRejectsUnknownKind()
    {
        var (service, interests, teams, academic) = CreateService();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        var member = new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, StudentId = Guid.NewGuid(), Role = "MEMBER", StudentCode = "SE000001", FullName = "Sinh viên" };
        teams.Teams.Add(team);
        teams.Members.Add(member);
        interests.Interests.Add(new TopicInterest { Id = Guid.NewGuid(), TeamId = team.Id, AdvisorKind = "LECTURER", AdvisorId = Guid.NewGuid(), Status = "OPEN" });
        var user = UserWith(studentId: member.StudentId, roles: Roles.Student);

        Assert.Single(await service.GetTeamInterestsAsync(team.Id, "lecturer", user, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.GetTeamInterestsAsync(team.Id, "unknown", user, CancellationToken.None));
    }

    [Fact]
    public async Task GetRecommendations_Uses_Matching_Order_And_Excludes_Unmatched_Teams()
    {
        var matching = new FakeTopicSuggestions();
        var (service, interestRepo, teamRepo, academic) = CreateService(matching);
        var lecturerId = Guid.NewGuid();
        var lecturerUser = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var lecturer = new LecturerSnapshot(
            lecturerId, "Thầy A", "thaya@fpt.edu.vn",
            [new CatalogItem(1, "D01", "Domain 1", true), new CatalogItem(2, "D02", "Domain 2", true)],
            null, null, true);
        academic.Lecturers[lecturerId] = lecturer;

        // Team 1: Domain 1, từng hướng dẫn ở EXE101
        var team1 = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE201", Name = "Nhóm 1", Status = "ACTIVE" };
        teamRepo.Teams.Add(team1);
        teamRepo.Domains.Add(new TeamDomain { TeamId = team1.Id, DomainId = 1, DomainCode = "D01", IsPrimary = true });
        interestRepo.Exe101Instructors.Add((team1.Id, lecturerId));

        // Team 2: Domain 1 và 2 (nhiều domain hơn nhưng không phải EXE101)
        var team2 = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE201", Name = "Nhóm 2", Status = "ACTIVE" };
        teamRepo.Teams.Add(team2);
        teamRepo.Domains.Add(new TeamDomain { TeamId = team2.Id, DomainId = 1, DomainCode = "D01", IsPrimary = true });
        teamRepo.Domains.Add(new TeamDomain { TeamId = team2.Id, DomainId = 2, DomainCode = "D02", IsPrimary = false });

        // Team 3: Domain 3 (không trùng)
        var team3 = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE201", Name = "Nhóm 3", Status = "ACTIVE" };
        teamRepo.Teams.Add(team3);
        teamRepo.Domains.Add(new TeamDomain { TeamId = team3.Id, DomainId = 3, DomainCode = "D03", IsPrimary = true });

        interestRepo.TeamIdsInSemester.AddRange([team1.Id, team2.Id, team3.Id]);
        matching.Items.AddRange([
            new TopicSuggestionItem(team1.Id, [1], 1, true),
            new TopicSuggestionItem(team2.Id, [1, 2], 0, false),
        ]);

        var response = await service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 10, lecturerUser, CancellationToken.None);

        Assert.Equal((academic.Semester.Id, "EXE201", "LECTURER", lecturerId, 1, 10), matching.Request);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(team1.Id, response.Items[0].Team.Id);
        Assert.True(response.Items[0].WasMyEXE101Team);

        Assert.Equal(team2.Id, response.Items[1].Team.Id);
        Assert.Equal(2, response.Items[1].MatchedDomains.Count);

        Assert.DoesNotContain(response.Items, x => x.Team.Id == team3.Id);
    }

    private static (TeamEntity Suggested, TeamEntity Other, TeamEntity Assigned, TeamEntity Disbanded) SeedAllScopeTeams(
        InMemoryTopicInterestRepo interestRepo, InMemoryTeamRepo teamRepo, FakeAcademic academic, Guid lecturerId)
    {
        TeamEntity Add(string name, string status, int domainId)
        {
            var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE201", Name = name, Status = status };
            teamRepo.Teams.Add(team);
            teamRepo.Domains.Add(new TeamDomain { TeamId = team.Id, DomainId = domainId, DomainCode = $"D0{domainId}", IsPrimary = true });
            interestRepo.TeamIdsInSemester.Add(team.Id);
            return team;
        }

        // "Nhóm B" trùng domain nên được gợi ý; "Nhóm A" (kế thừa từ import mốc, Matching chưa có domain) thì không.
        var other = Add("Nhóm A", "ACTIVE", 3);
        var suggested = Add("Nhóm B", "ACTIVE", 1);
        var assigned = Add("Nhóm C", "ACTIVE", 3);
        var disbanded = Add("Nhóm D", "DISBANDED", 3);
        interestRepo.AssignedLecturerTeamIds.Add(assigned.Id);
        interestRepo.Interests.Add(new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = other.Id, AdvisorKind = "LECTURER", AdvisorId = Guid.NewGuid(), Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        interestRepo.Interests.Add(new TopicInterest
        {
            Id = Guid.NewGuid(), TeamId = other.Id, AdvisorKind = "LECTURER", AdvisorId = lecturerId, Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        return (suggested, other, assigned, disbanded);
    }

    [Fact]
    public async Task GetRecommendations_ScopeAll_ListsEveryTeamWithoutAdvisor_SuggestedFirst()
    {
        var matching = new FakeTopicSuggestions();
        var (service, interestRepo, teamRepo, academic) = CreateService(matching);
        var lecturerId = Guid.NewGuid();
        var (suggested, other, assigned, disbanded) = SeedAllScopeTeams(interestRepo, teamRepo, academic, lecturerId);
        matching.Items.Add(new TopicSuggestionItem(suggested.Id, [1], 0, false));

        var response = await service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 20,
            UserWith(lecturerId: lecturerId, roles: Roles.Lecturer), CancellationToken.None, "all");

        Assert.Equal([suggested.Id, other.Id], response.Items.Select(i => i.Team.Id));
        Assert.Single(response.Items[0].MatchedDomains);
        Assert.Empty(response.Items[1].MatchedDomains);
        Assert.Equal(1, response.Items[1].OtherInterestedCount); // không tính quan tâm của chính mình
        Assert.NotNull(response.Items[1].MyInterest);             // hoàn tác được ngay trên thẻ
        Assert.DoesNotContain(response.Items, i => i.Team.Id == assigned.Id || i.Team.Id == disbanded.Id);
        Assert.Equal(100, matching.Request!.Value.PageSize);
    }

    [Fact]
    public async Task GetRecommendations_ScopeAll_Paginates_AndSurvivesMatchingOutage()
    {
        var matching = new FakeTopicSuggestions { Failure = MatchingErrors.Unavailable() };
        var (service, interestRepo, teamRepo, academic) = CreateService(matching);
        var lecturerId = Guid.NewGuid();
        var (suggested, other, _, _) = SeedAllScopeTeams(interestRepo, teamRepo, academic, lecturerId);
        var user = UserWith(lecturerId: lecturerId, roles: Roles.Lecturer);

        var first = await service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 1, user, CancellationToken.None, "ALL");
        var second = await service.GetRecommendationsAsync("LECTURER", "EXE201", 2, 1, user, CancellationToken.None, "all");

        Assert.Equal(other.Id, Assert.Single(first.Items).Team.Id); // Matching lỗi: xếp theo tên
        Assert.Equal(suggested.Id, Assert.Single(second.Items).Team.Id);
        Assert.All(first.Items.Concat(second.Items), i => Assert.Empty(i.MatchedDomains));
    }

    [Fact]
    public async Task GetRecommendations_SuggestedScope_StillFailsWhenMatchingIsDown_AndRejectsUnknownScope()
    {
        var matching = new FakeTopicSuggestions { Failure = MatchingErrors.Unavailable() };
        var (service, _, _, _) = CreateService(matching);
        var user = UserWith(lecturerId: Guid.NewGuid(), roles: Roles.Lecturer);

        await Assert.ThrowsAsync<HttpStatusException>(() =>
            service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 20, user, CancellationToken.None, "suggested"));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.GetRecommendationsAsync("LECTURER", "EXE201", 1, 20, user, CancellationToken.None, "everything"));
    }

    [Fact]
    public async Task OpenInterest_Fails_When_FormationRoundStillOpen_ForMentor()
    {
        var (service, _, teamRepo, academic) = CreateService();
        academic.OpenCourses.Add("EXE101");
        var mentorId = Guid.NewGuid();
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None));
        Assert.Equal("interest-closed", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_MentoringWeekStarted_ForMentor()
    {
        var (service, _, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-50)), 6);

        var mentorId = Guid.NewGuid();
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None));
        Assert.Equal("interest-closed", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_SemesterConfigMissing_ForMentor()
    {
        var (service, _, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear(); // Đợt lập nhóm đã đóng
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", null, null); // Thiếu StartDate và MentoringStartWeek

        var mentorId = Guid.NewGuid();
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None));
        Assert.Equal("interest-closed", ex.Code);
        Assert.Contains("chưa cấu hình ngày bắt đầu", ex.Message);
    }

    [Fact]
    public async Task OpenInterest_Succeeds_When_FormationRoundClosed_And_BeforeMentoringWeek_ForMentor()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), 6);

        var mentorId = Guid.NewGuid();
        academic.Mentors[mentorId] = new MentorSnapshot(mentorId, "Mentor", "mentor@example.test", [], 5, null, true);
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        var response = await service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None);

        Assert.Equal("OPEN", response.Status);
        Assert.Equal("MENTOR", response.AdvisorKind);
        Assert.Equal(mentorId, response.AdvisorId);

        var outbox = Assert.Single(interestRepo.OutboxMessages);
        Assert.Equal("TopicInterestOpened", outbox.EventType);
        using var doc = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("MENTOR", doc.RootElement.GetProperty("advisorKind").GetString());
    }

    [Fact]
    public async Task OpenInterest_Fails_When_LimitReached_ForMentor()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), 6);

        var mentorId = Guid.NewGuid();
        academic.Mentors[mentorId] = new MentorSnapshot(mentorId, "Mentor", "mentor@example.test", [], 5, null, true);
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);

        for (var i = 0; i < 7; i++)
        {
            var interestedTeamId = Guid.NewGuid();
            interestRepo.TeamScopes[interestedTeamId] = (academic.Semester.Id, "EXE201");
            interestRepo.Interests.Add(new TopicInterest
            {
                Id = Guid.NewGuid(), TeamId = interestedTeamId, AdvisorKind = "MENTOR", AdvisorId = mentorId, Status = "OPEN"
            });
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None));
        Assert.Equal("interest-limit", ex.Code);
    }

    [Fact]
    public async Task OpenInterest_Fails_When_MentorCapacityIsUnavailable()
    {
        var (service, interests, teams, academic) = CreateService();
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), 6);
        var mentorId = Guid.NewGuid();
        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teams.Teams.Add(team);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), UserWith(mentorId: mentorId, roles: Roles.Mentor), CancellationToken.None));
        Assert.Empty(interests.Interests);
    }

    [Fact]
    public async Task OpenInterest_Uses_Mentor_MaxTeams_Regardless_Of_Preferences_And_Assignments()
    {
        var (service, interestRepo, teamRepo, academic) = CreateService();
        academic.OpenCourses.Clear();
        academic.Semester = new SemesterDto(Guid.NewGuid(), "SP26", "Spring 2026", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), 6);

        var mentorId = Guid.NewGuid();
        var mentorUser = UserWith(mentorId: mentorId, roles: Roles.Mentor);
        // Tối đa 5 nhóm, mong muốn 2, đã nhận 3: vẫn được mở tối đa 7 quan tâm.
        academic.Mentors[mentorId] = new MentorSnapshot(mentorId, "Mentor X", "mentorx@fpt.edu.vn", [], 5, 2, true);
        interestRepo.AssignedCounts[("MENTOR", mentorId)] = 3;

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm A", Status = "ACTIVE" };
        teamRepo.Teams.Add(team);
        interestRepo.TeamScopes[team.Id] = (academic.Semester.Id, team.CourseCode);

        for (var i = 0; i < 6; i++)
        {
            var interestedTeamId = Guid.NewGuid();
            interestRepo.TeamScopes[interestedTeamId] = (academic.Semester.Id, "EXE201");
            interestRepo.Interests.Add(new TopicInterest
            {
                Id = Guid.NewGuid(), TeamId = interestedTeamId, AdvisorKind = "MENTOR", AdvisorId = mentorId, Status = "OPEN"
            });
        }

        await service.OpenInterestAsync(team.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None);

        var anotherTeam = new TeamEntity { Id = Guid.NewGuid(), SemesterId = academic.Semester.Id, CourseCode = "EXE101", Name = "Nhóm B", Status = "ACTIVE" };
        teamRepo.Teams.Add(anotherTeam);
        interestRepo.TeamScopes[anotherTeam.Id] = (academic.Semester.Id, anotherTeam.CourseCode);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenInterestAsync(anotherTeam.Id, new OpenTopicInterestInput("MENTOR"), mentorUser, CancellationToken.None));
        Assert.Equal("interest-limit", ex.Code);
    }
}
