using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AuditLog;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.LecturerPreferences;
using Team.Services.Teams;

namespace Team.Tests;

public sealed class LecturerPreferencesTests
{
    private sealed class InMemoryLecturerPreferenceRepo : ILecturerPreferenceRepository
    {
        public List<LecturerPreference> Preferences { get; } = [];
        public List<TopicInterest> Interests { get; } = [];
        public List<LecturerAssignment> Assignments { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];
        public LecturerPreferencePreviousInstructor? PreviousInstructor { get; set; }

        public Task<IReadOnlyList<LecturerPreference>> ListByTeamIdAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LecturerPreference>>(Preferences.Where(p => p.TeamId == teamId).OrderBy(p => p.Rank).ToList());

        public Task<IReadOnlyList<LecturerPreference>> ListByLecturerIdAsync(Guid lecturerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LecturerPreference>>(Preferences.Where(p => p.LecturerId == lecturerId).OrderBy(p => p.Rank).ToList());

        public Task<bool> HasAssignedInstructorAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Assignments.Any(a => a.TeamId == teamId && a.Status == "ASSIGNED"));

        public Task<IReadOnlyList<TopicInterest>> ListOpenLecturerInterestsByTeamIdAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TopicInterest>>(Interests.Where(t => t.TeamId == teamId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER").OrderBy(t => t.CreatedAt).ToList());

        public Task<bool> IsLecturerInterestedAsync(Guid teamId, Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(Interests.Any(t => t.TeamId == teamId && t.AdvisorId == lecturerId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER"));

        public Task<LecturerPreferencePreviousInstructor?> GetPreviousInstructorAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(PreviousInstructor);

        public Task<HashSet<Guid>> GetInterestedTeamIdsAsync(Guid lecturerId, IEnumerable<Guid> teamIds, CancellationToken ct) =>
            Task.FromResult(Interests
                .Where(t => teamIds.Contains(t.TeamId) && t.AdvisorId == lecturerId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER")
                .Select(t => t.TeamId)
                .ToHashSet());

        public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Topic = BuildingBlocks.Messaging.Topics.Assignment,
                EventType = eventType,
                AggregateType = "Team",
                AggregateId = teamId,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload, BuildingBlocks.Messaging.EventEnvelope.JsonOptions),
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        public Task ReplacePreferencesAsync(
            Guid teamId,
            IReadOnlyList<LecturerPreference> preferences,
            CancellationToken ct)
        {
            Preferences.RemoveAll(p => p.TeamId == teamId);
            Preferences.AddRange(preferences);
            return Task.CompletedTask;
        }
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
            if (team == null) return Task.FromResult<TeamGraph?>(null);
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
                if (team != null)
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
        public SemesterDto Semester { get; } = new(Guid.NewGuid(), "SP26", "Spring 2026");
        public RoundDto? OpenRound { get; set; }
        public Dictionary<Guid, LecturerSnapshot> Lecturers { get; } = [];

        public FakeAcademic()
        {
            OpenRound = new RoundDto(Guid.NewGuid(), Semester.Id, "EXE101", RoundTypes.TeamFormation, DateTimeOffset.UtcNow.AddDays(5), "OPEN");
        }

        public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => Task.FromResult<SemesterDto?>(Semester);

        public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct)
        {
            if (OpenRound is not null && OpenRound.CourseCode == courseCode && OpenRound.RoundType == roundType)
            {
                return Task.FromResult<RoundDto?>(OpenRound);
            }
            return Task.FromResult<RoundDto?>(null);
        }

        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

        public Task<LecturerSnapshot?> GetLecturerAsync(Guid lecturerId, CancellationToken ct) =>
            Task.FromResult(Lecturers.GetValueOrDefault(lecturerId));

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

    private static (LecturerPreferencesService Service, InMemoryLecturerPreferenceRepo PrefRepo, InMemoryTeamRepo TeamRepo, FakeAcademic Academic, TeamEntity Team, Guid LeaderId) CreateTestSetup(string courseCode = "EXE101")
    {
        var academic = new FakeAcademic();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), academic.Semester.Id, courseCode, RoundTypes.TeamFormation, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        var prefRepo = new InMemoryLecturerPreferenceRepo();
        var teamRepo = new InMemoryTeamRepo();

        var teamId = Guid.NewGuid();
        var semesterId = academic.Semester.Id;
        var leaderId = Guid.NewGuid();

        var team = new TeamEntity
        {
            Id = teamId,
            SemesterId = semesterId,
            CourseCode = courseCode,
            Name = "Nhóm Alpha",
            Status = TeamStatuses.Active,
            MemberCount = 4,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        teamRepo.Teams.Add(team);

        var leader = new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SemesterId = semesterId,
            StudentId = leaderId,
            StudentCode = "SE160001",
            FullName = "Nguyễn Văn Trưởng",
            SpecializationId = 1,
            Role = MemberRoles.Leader,
            JoinSource = "CREATED",
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-2),
        };
        teamRepo.Members.Add(leader);

        for (int i = 2; i <= 4; i++)
        {
            teamRepo.Members.Add(new TeamMember
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                SemesterId = semesterId,
                StudentId = Guid.NewGuid(),
                StudentCode = $"SE16000{i}",
                FullName = $"Thành viên {i}",
                SpecializationId = 1,
                Role = MemberRoles.Member,
                JoinSource = "INVITATION",
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-2),
            });
        }

        var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);
        var audit = new AuditLogWriter(db);
        var reader = new TeamReader(teamRepo, academic);
        var rounds = new RoundGuard(academic);
        var service = new LecturerPreferencesService(prefRepo, teamRepo, academic, rounds, reader, audit, TimeProvider.System);

        return (service, prefRepo, teamRepo, academic, team, leaderId);
    }

    private static ClaimsPrincipal StudentUser(Guid studentId, bool isLeader = true) =>
        new(new ClaimsIdentity([
            new(ClaimNames.Subject, Guid.NewGuid().ToString()),
            new(ClaimNames.StudentId, studentId.ToString()),
            new(ClaimTypes.Role, Roles.Student)
        ], "test"));

    private static ClaimsPrincipal LecturerUser(Guid lecturerId) =>
        new(new ClaimsIdentity([
            new(ClaimNames.Subject, Guid.NewGuid().ToString()),
            new(ClaimNames.LecturerId, lecturerId.ToString()),
            new(ClaimTypes.Role, Roles.Lecturer)
        ], "test"));

    private static ClaimsPrincipal AdminUser() =>
        new(new ClaimsIdentity([
            new(ClaimNames.Subject, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, Roles.Admin)
        ], "test"));

    [Fact]
    public async Task GetPreferences_WhenTeamNotFound_ThrowsNotFound()
    {
        var (service, _, _, _, _, leaderId) = CreateTestSetup();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetPreferencesAsync(Guid.NewGuid(), StudentUser(leaderId), CancellationToken.None));
    }

    [Fact]
    public async Task GetPreferences_WhenCallerNotMemberNorAdvisor_ThrowsNotMember()
    {
        var (service, _, _, _, team, _) = CreateTestSetup();
        var strangerStudentId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetPreferencesAsync(team.Id, StudentUser(strangerStudentId), CancellationToken.None));
    }

    [Fact]
    public async Task GetPreferences_Returns_Preferences_And_Interested_Lecturers()
    {
        var (service, prefRepo, _, academic, team, leaderId) = CreateTestSetup();

        var lec1 = Guid.NewGuid();
        var lec2 = Guid.NewGuid();
        var lec3 = Guid.NewGuid();

        academic.Lecturers[lec1] = new LecturerSnapshot(lec1, "TS. Lê Hoàng", "hoang@fpt.edu.vn", [new CatalogItem(1, "D01", "Fintech", true)], 10, 10, true);
        academic.Lecturers[lec2] = new LecturerSnapshot(lec2, "ThS. Trần Bình", "binh@fpt.edu.vn", [new CatalogItem(2, "D02", "Edtech", true)], 10, 10, true);
        academic.Lecturers[lec3] = new LecturerSnapshot(lec3, "TS. Phạm Mai", "mai@fpt.edu.vn", [], 10, 10, true);

        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team.Id,
            Rank = 1,
            LecturerId = lec1,
            LecturerName = "TS. Lê Hoàng",
            UpdatedByStudentId = leaderId,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team.Id,
            Rank = 2,
            LecturerId = lec2,
            LecturerName = "ThS. Trần Bình",
            UpdatedByStudentId = leaderId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // lec1 and lec3 marked interest
        prefRepo.Interests.Add(new TopicInterest
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            AdvisorKind = "LECTURER",
            AdvisorId = lec1,
            AdvisorName = "TS. Lê Hoàng",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow
        });
        prefRepo.Interests.Add(new TopicInterest
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            AdvisorKind = "LECTURER",
            AdvisorId = lec3,
            AdvisorName = "TS. Phạm Mai",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var res = await service.GetPreferencesAsync(team.Id, StudentUser(leaderId), CancellationToken.None);

        Assert.Equal(3, res.MaxPreferences);
        Assert.False(res.Locked);
        Assert.NotNull(res.LockAt);

        Assert.Equal(2, res.Preferences.Count);
        Assert.Equal(lec1, res.Preferences[0].LecturerId);
        Assert.Equal("TS. Lê Hoàng", res.Preferences[0].FullName);
        Assert.Equal(1, res.Preferences[0].Rank);
        Assert.True(res.Preferences[0].IsInterested); // lec1 marked interest

        Assert.Equal(lec2, res.Preferences[1].LecturerId);
        Assert.Equal(2, res.Preferences[1].Rank);
        Assert.False(res.Preferences[1].IsInterested); // lec2 did not mark interest

        Assert.Equal(2, res.InterestedLecturers.Count);
        Assert.Contains(res.InterestedLecturers, l => l.LecturerId == lec1);
        Assert.Contains(res.InterestedLecturers, l => l.LecturerId == lec3);
    }

    [Fact]
    public async Task GetPreferences_WhenAssignedInstructor_ReturnsLockedTrue()
    {
        var (service, prefRepo, _, _, team, leaderId) = CreateTestSetup();
        prefRepo.Assignments.Add(new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            LecturerId = Guid.NewGuid(),
            LecturerName = "GV Phân Công",
            Status = "ASSIGNED"
        });

        var res = await service.GetPreferencesAsync(team.Id, StudentUser(leaderId), CancellationToken.None);

        Assert.True(res.Locked);
    }

    [Fact]
    public async Task GetPreferences_WhenRoundClosed_ReturnsLockedTrue()
    {
        var (service, _, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = null; // round closed

        var res = await service.GetPreferencesAsync(team.Id, StudentUser(leaderId), CancellationToken.None);

        Assert.True(res.Locked);
    }

    [Fact]
    public async Task GetPreferences_ForExe201_ReturnsPreviousInstructor()
    {
        var (service, prefRepo, _, academic, team, leaderId) = CreateTestSetup("EXE201");
        var prevLecId = Guid.NewGuid();
        academic.Lecturers[prevLecId] = new LecturerSnapshot(prevLecId, "TS. Cựu Hướng Dẫn", "prev@fpt.edu.vn", [], 10, 10, true);

        prefRepo.PreviousInstructor = new LecturerPreferencePreviousInstructor(prevLecId, "TS. Cựu Hướng Dẫn");

        var res = await service.GetPreferencesAsync(team.Id, StudentUser(leaderId), CancellationToken.None);

        Assert.NotNull(res.PreviousInstructor);
        Assert.Equal(prevLecId, res.PreviousInstructor.LecturerId);
        Assert.Equal("TS. Cựu Hướng Dẫn", res.PreviousInstructor.FullName);
    }

    [Fact]
    public async Task UpdatePreferences_WhenCallerNotLeader_ThrowsNotLeader()
    {
        var (service, _, _, _, team, _) = CreateTestSetup();
        var memberId = Guid.NewGuid();

        var input = new UpdateLecturerPreferencesInput([Guid.NewGuid()]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdatePreferencesAsync(team.Id, input, StudentUser(memberId), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePreferences_WhenRoundClosed_ThrowsRoundClosed()
    {
        var (service, _, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = null;

        var input = new UpdateLecturerPreferencesInput([Guid.NewGuid()]);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdatePreferencesAsync(team.Id, input, StudentUser(leaderId), CancellationToken.None));
        Assert.Equal("round-closed", ex.Code);
    }

    [Fact]
    public async Task UpdatePreferences_WhenAlreadyAssigned_ThrowsPreferencesLocked()
    {
        var (service, prefRepo, _, _, team, leaderId) = CreateTestSetup();
        prefRepo.Assignments.Add(new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            LecturerId = Guid.NewGuid(),
            LecturerName = "GV Hướng Dẫn",
            Status = "ASSIGNED"
        });

        var input = new UpdateLecturerPreferencesInput([Guid.NewGuid()]);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdatePreferencesAsync(team.Id, input, StudentUser(leaderId), CancellationToken.None));
        Assert.Equal("preferences-locked", ex.Code);
    }

    [Fact]
    public async Task UpdatePreferences_WhenOver3Preferences_ThrowsValidation()
    {
        var (service, _, _, _, team, leaderId) = CreateTestSetup();
        var input = new UpdateLecturerPreferencesInput([
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
        ]);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.UpdatePreferencesAsync(team.Id, input, StudentUser(leaderId), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePreferences_WhenDuplicateLecturers_ThrowsValidation()
    {
        var (service, _, _, _, team, leaderId) = CreateTestSetup();
        var dup = Guid.NewGuid();
        var input = new UpdateLecturerPreferencesInput([dup, dup]);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.UpdatePreferencesAsync(team.Id, input, StudentUser(leaderId), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePreferences_Succeeds_AndPublishesOutbox_WithAddedLecturers()
    {
        var (service, prefRepo, _, academic, team, leaderId) = CreateTestSetup();
        var oldLec = Guid.NewGuid();
        var newLec1 = Guid.NewGuid();
        var newLec2 = Guid.NewGuid();

        academic.Lecturers[oldLec] = new LecturerSnapshot(oldLec, "TS. Cũ", "old@fpt.edu.vn", [], 10, 10, true);
        academic.Lecturers[newLec1] = new LecturerSnapshot(newLec1, "TS. Mới 1", "new1@fpt.edu.vn", [], 10, 10, true);
        academic.Lecturers[newLec2] = new LecturerSnapshot(newLec2, "TS. Mới 2", "new2@fpt.edu.vn", [], 10, 10, true);

        // Ban đầu nhóm đã chọn oldLec ở nguyện vọng 1
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team.Id,
            Rank = 1,
            LecturerId = oldLec,
            LecturerName = "TS. Cũ",
            UpdatedByStudentId = leaderId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Đổi thành: [newLec1, oldLec, newLec2]
        var input = new UpdateLecturerPreferencesInput([newLec1, oldLec, newLec2]);
        var res = await service.UpdatePreferencesAsync(team.Id, input, StudentUser(leaderId), CancellationToken.None);

        Assert.Equal(3, res.Preferences.Count);
        Assert.Equal(newLec1, res.Preferences[0].LecturerId);
        Assert.Equal(1, res.Preferences[0].Rank);
        Assert.Equal(oldLec, res.Preferences[1].LecturerId);
        Assert.Equal(2, res.Preferences[1].Rank);
        Assert.Equal(newLec2, res.Preferences[2].LecturerId);
        Assert.Equal(3, res.Preferences[2].Rank);

        // Check Outbox
        Assert.Single(prefRepo.OutboxMessages);
        var msg = prefRepo.OutboxMessages[0];
        Assert.Equal(BuildingBlocks.Messaging.Topics.Assignment, msg.Topic);
        Assert.Equal(AssignmentEventTypes.LecturerPreferencesUpdated, msg.EventType);

        // Verify camelCase JSON serialization (contract requirement in events.md)
        Assert.Contains("\"addedLecturerIds\":", msg.Payload);
        Assert.Contains("\"lecturerIds\":", msg.Payload);
        Assert.Contains("\"teamId\":", msg.Payload);

        var payload = System.Text.Json.JsonSerializer.Deserialize<LecturerPreferencesUpdated>(
            msg.Payload,
            BuildingBlocks.Messaging.EventEnvelope.JsonOptions);
        Assert.NotNull(payload);
        Assert.Equal(team.Id, payload.TeamId);
        Assert.Equal([newLec1, oldLec, newLec2], payload.LecturerIds);
        // AddedLecturerIds should only contain newLec1 and newLec2 (oldLec was already present)
        Assert.Equal([newLec1, newLec2], payload.AddedLecturerIds);
    }

    [Fact]
    public async Task GetPreferencesMe_WhenNotLecturer_ThrowsForbidden()
    {
        var (service, _, _, _, _, leaderId) = CreateTestSetup();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetPreferencesMeAsync(null, StudentUser(leaderId), CancellationToken.None));
    }

    [Fact]
    public async Task GetPreferencesMe_Returns_RankedTeams_With_IAmInterested()
    {
        var (service, prefRepo, teamRepo, _, team, leaderId) = CreateTestSetup("EXE101");
        var myLecturerId = Guid.NewGuid();

        // Team 1 ranked lecturer as #2
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team.Id,
            Rank = 2,
            LecturerId = myLecturerId,
            LecturerName = "Tôi",
            UpdatedByStudentId = leaderId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Team 2 ranked lecturer as #1
        var team2Id = Guid.NewGuid();
        var team2 = new TeamEntity
        {
            Id = team2Id,
            SemesterId = team.SemesterId,
            CourseCode = "EXE101",
            Name = "Nhóm Beta",
            Status = TeamStatuses.Active,
            MemberCount = 4,
            CreatedAt = DateTimeOffset.UtcNow
        };
        teamRepo.Teams.Add(team2);
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team2Id,
            Rank = 1,
            LecturerId = myLecturerId,
            LecturerName = "Tôi",
            UpdatedByStudentId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Lecturer marked interest in Team 1, but not Team 2
        prefRepo.Interests.Add(new TopicInterest
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            AdvisorKind = "LECTURER",
            AdvisorId = myLecturerId,
            AdvisorName = "Tôi",
            Status = "OPEN",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var res = await service.GetPreferencesMeAsync(null, LecturerUser(myLecturerId), CancellationToken.None);

        Assert.Equal(2, res.Count);
        // Should be sorted by rank asc (Rank 1 first, then Rank 2)
        Assert.Equal(team2Id, res[0].Team.Id);
        Assert.Equal(1, res[0].Rank);
        Assert.False(res[0].IAmInterested);

        Assert.Equal(team.Id, res[1].Team.Id);
        Assert.Equal(2, res[1].Rank);
        Assert.True(res[1].IAmInterested);
    }

    [Fact]
    public async Task GetPreferencesMe_FiltersByCourseCode()
    {
        var (service, prefRepo, teamRepo, _, team, leaderId) = CreateTestSetup("EXE101");
        var myLecturerId = Guid.NewGuid();

        // Team 1: EXE101
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team.Id,
            Rank = 1,
            LecturerId = myLecturerId,
            LecturerName = "Tôi",
            UpdatedByStudentId = leaderId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Team 2: EXE201
        var team2Id = Guid.NewGuid();
        teamRepo.Teams.Add(new TeamEntity
        {
            Id = team2Id,
            SemesterId = team.SemesterId,
            CourseCode = "EXE201",
            Name = "Nhóm EXE201",
            Status = TeamStatuses.Active,
            MemberCount = 4,
            CreatedAt = DateTimeOffset.UtcNow
        });
        prefRepo.Preferences.Add(new LecturerPreference
        {
            TeamId = team2Id,
            Rank = 1,
            LecturerId = myLecturerId,
            LecturerName = "Tôi",
            UpdatedByStudentId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var resExe101 = await service.GetPreferencesMeAsync("EXE101", LecturerUser(myLecturerId), CancellationToken.None);
        Assert.Single(resExe101);
        Assert.Equal(team.Id, resExe101[0].Team.Id);

        var resExe201 = await service.GetPreferencesMeAsync("EXE201", LecturerUser(myLecturerId), CancellationToken.None);
        Assert.Single(resExe201);
        Assert.Equal(team2Id, resExe201[0].Team.Id);
    }
}
