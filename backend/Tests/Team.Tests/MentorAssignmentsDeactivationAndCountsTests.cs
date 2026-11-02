using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.AdvisorAssignments;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// Kiểm thử tính năng BE-74:
/// 1. Gỡ mentor khi Bộ môn khóa tài khoản (isActive=false): ghi nhật ký MENTOR_UNASSIGNED và phát MentorUnassigned qua Outbox (idempotent).
/// 2. API nội bộ POST /internal/mentors/team-counts đếm số nhóm đang mentor cho danh sách mentor (Academic W27), bỏ nhóm đã giải tán.
/// </summary>
public sealed class MentorAssignmentsDeactivationAndCountsTests : IAsyncLifetime
{
    private readonly TeamApiFactory _api = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    private sealed class InMemoryMentorRepo : IMentorAssignmentRepository
    {
        public List<MentorAssignment> Assignments { get; } = [];

        /// <summary>Nhóm theo id (học kỳ, trạng thái) – thay cho join với <c>team.teams</c> của repository thật.</summary>
        public Dictionary<Guid, (Guid SemesterId, string Status)> Teams { get; } = [];

        public int SaveCount { get; private set; }

        public Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Assignments.FirstOrDefault(a => a.TeamId == teamId && a.Status == "ASSIGNED"));

        public Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct) =>
            Task.FromResult(Assignments.Where(a => a.MentorId == mentorId && a.Status == "ASSIGNED").ToList());

        public Task<List<MentorAssignment>> ListActiveByMentorIdTrackedAsync(Guid mentorId, CancellationToken ct) =>
            Task.FromResult(Assignments.Where(a => a.MentorId == mentorId && a.Status == "ASSIGNED").ToList());

        // Cùng điều kiện với MentorAssignmentRepository: dòng ASSIGNED, nhóm tồn tại và chưa DISBANDED, lọc học kỳ nếu có.
        public Task<Dictionary<Guid, int>> CountActiveTeamsByMentorIdsAsync(IReadOnlyList<Guid> mentorIds, Guid? semesterId, CancellationToken ct)
        {
            var counts = Assignments
                .Where(a => mentorIds.Contains(a.MentorId) && a.Status == "ASSIGNED")
                .Where(a => Teams.TryGetValue(a.TeamId, out var t) && t.Status != "DISBANDED"
                    && (semesterId is null || t.SemesterId == semesterId))
                .GroupBy(a => a.MentorId)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(counts);
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Guid AddTeam(Guid semesterId, string status = "ACTIVE")
        {
            var id = Guid.NewGuid();
            Teams[id] = (semesterId, status);
            return id;
        }
    }

    private sealed class StubLecturerRepo : ILecturerAssignmentRepository
    {
        public Task<LecturerAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) => Task.FromResult<LecturerAssignment?>(null);
        public Task<List<LecturerAssignment>> ListActiveByLecturerIdAsync(Guid lecturerId, CancellationToken ct) => Task.FromResult<List<LecturerAssignment>>([]);
    }

    private sealed class StubTeamRepo : ITeamRepository
    {
        public Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct) => Task.FromResult<TeamEntity?>(null);
        public Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct) => Task.FromResult<TeamGraph?>(null);
        public Task<List<TeamGraph>> ListGraphsAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct) => Task.FromResult<List<TeamGraph>>([]);
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

    [Fact]
    public async Task HandleMentorRegistered_WhenIsActiveFalse_UnassignsAllTeamsAndEmitsOutboxEvents()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var lecturerRepo = new StubLecturerRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var harness = MentorWriterHarness.Create();
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader, harness.Writer);

        var mentorId = Guid.NewGuid();
        var teamId1 = Guid.NewGuid();
        var teamId2 = Guid.NewGuid();

        var assignment1 = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId1,
            MentorId = mentorId,
            MentorName = "Lê Văn Tuấn",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var assignment2 = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId2,
            MentorId = mentorId,
            MentorName = "Lê Văn Tuấn",
            Status = "ASSIGNED",
            Reason = "DOMAIN",
            Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        mentorRepo.Assignments.Add(assignment1);
        mentorRepo.Assignments.Add(assignment2);

        // Act: Bộ môn khóa mentor (isActive = false)
        var unassignedCount = await service.HandleMentorRegisteredAsync(
            new MentorRegisteredEvent(MentorId: mentorId, IsActive: false),
            CancellationToken.None);

        // Assert: 2 nhóm được gỡ
        Assert.Equal(2, unassignedCount);
        Assert.Equal("ENDED", assignment1.Status);
        Assert.NotNull(assignment1.EndedAt);
        Assert.Equal("MENTOR_DEACTIVATED", assignment1.EndReason);

        Assert.Equal("ENDED", assignment2.Status);
        Assert.NotNull(assignment2.EndedAt);
        Assert.Equal("MENTOR_DEACTIVATED", assignment2.EndReason);

        // Đúng 2 sự kiện MentorUnassigned trong Outbox, cùng một lần lưu với dòng gán và nhật ký
        Assert.Equal(1, mentorRepo.SaveCount);
        var outbox = harness.Outbox();
        Assert.Equal(2, outbox.Count);
        var msg1 = Assert.Single(outbox.Where(m => m.AggregateId == teamId1));
        var msg2 = Assert.Single(outbox.Where(m => m.AggregateId == teamId2));

        Assert.Equal(Topics.Assignment, msg1.Topic);
        Assert.Equal("MentorUnassigned", msg1.EventType);
        Assert.Equal("Team", msg1.AggregateType);
        Assert.Equal(teamId1, msg1.AggregateId);

        Assert.Equal(Topics.Assignment, msg2.Topic);
        Assert.Equal("MentorUnassigned", msg2.EventType);
        Assert.Equal("Team", msg2.AggregateType);
        Assert.Equal(teamId2, msg2.AggregateId);

        // Kiểm tra JSON payload thực tế theo docs/contracts/events.md (JSON camelCase)
        using var doc1 = JsonDocument.Parse(msg1.Payload);
        var root1 = doc1.RootElement;
        Assert.True(root1.TryGetProperty("assignmentId", out var pAssignmentId));
        Assert.Equal(assignment1.Id, pAssignmentId.GetGuid());
        Assert.True(root1.TryGetProperty("teamId", out var pTeamId));
        Assert.Equal(teamId1, pTeamId.GetGuid());
        Assert.True(root1.TryGetProperty("mentorId", out var pMentorId));
        Assert.Equal(mentorId, pMentorId.GetGuid());
        Assert.True(root1.TryGetProperty("reason", out var pReason));
        Assert.Equal("MENTOR_DEACTIVATED", pReason.GetString());

        // Nhật ký nhóm MENTOR_UNASSIGNED cho từng nhóm, người thực hiện là hệ thống (rule 10)
        var audits = harness.Audits();
        Assert.Equal(2, audits.Count);
        foreach (var (assignment, teamId) in new[] { (assignment1, teamId1), (assignment2, teamId2) })
        {
            var log = Assert.Single(audits.Where(a => a.TeamId == teamId));
            Assert.Equal("MENTOR_UNASSIGNED", log.Action);
            Assert.Equal("MentorAssignment", log.EntityType);
            Assert.Equal(assignment.Id, log.EntityId);
            Assert.Null(log.ActorAccountId);
            Assert.Equal("System", log.ActorRole);
            Assert.Equal("MENTOR_DEACTIVATED", log.Reason);
            Assert.Contains(mentorId.ToString(), log.OldValues);
        }
    }

    [Fact]
    public async Task HandleMentorRegistered_WhenReceivedAgain_IsIdempotent()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var lecturerRepo = new StubLecturerRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var harness = MentorWriterHarness.Create();
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader, harness.Writer);

        var mentorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var assignment = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Lê Văn Tuấn",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            Source = "AUTO"
        };
        mentorRepo.Assignments.Add(assignment);

        // Lần 1: gỡ thành công
        var count1 = await service.HandleMentorRegisteredAsync(new MentorRegisteredEvent(mentorId, IsActive: false), CancellationToken.None);
        Assert.Equal(1, count1);
        Assert.Single(harness.Outbox());
        Assert.Single(harness.Audits());

        // Lần 2 (nhận lại cùng sự kiện): trạng thái và số sự kiện không đổi
        var count2 = await service.HandleMentorRegisteredAsync(new MentorRegisteredEvent(mentorId, IsActive: false), CancellationToken.None);
        Assert.Equal(0, count2);
        Assert.Equal("ENDED", assignment.Status);
        Assert.Single(harness.Outbox()); // Vẫn chỉ có 1 message, không tạo trùng
        Assert.Single(harness.Audits()); // và chỉ 1 dòng nhật ký
        Assert.Equal(1, mentorRepo.SaveCount); // lần 2 không lưu gì
    }

    [Fact]
    public async Task HandleMentorRegistered_WhenIsActiveTrue_DoesNotUnassignOrEmit()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var lecturerRepo = new StubLecturerRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var harness = MentorWriterHarness.Create();
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader, harness.Writer);

        var mentorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var assignment = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Lê Văn Tuấn",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            Source = "AUTO"
        };
        mentorRepo.Assignments.Add(assignment);

        // Act: Sự kiện cập nhật mentor nhưng isActive = true
        var count = await service.HandleMentorRegisteredAsync(new MentorRegisteredEvent(mentorId, IsActive: true), CancellationToken.None);

        // Assert: Không gỡ, không phát sự kiện
        Assert.Equal(0, count);
        Assert.Equal("ASSIGNED", assignment.Status);
        Assert.Null(assignment.EndedAt);
        Assert.Empty(harness.Outbox());
        Assert.Empty(harness.Audits());
    }

    [Fact]
    public async Task GetMentorTeamCounts_ReturnsCountsForMultipleMentorsAndZeroForUnassigned()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var lecturerRepo = new StubLecturerRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var harness = MentorWriterHarness.Create();
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader, harness.Writer);

        var mentor1 = Guid.NewGuid();
        var mentor2 = Guid.NewGuid();
        var mentor3 = Guid.NewGuid(); // không có nhóm

        var semester = Guid.NewGuid();
        mentorRepo.Assignments.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = mentorRepo.AddTeam(semester), MentorId = mentor1, Status = "ASSIGNED" });
        mentorRepo.Assignments.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = mentorRepo.AddTeam(semester), MentorId = mentor1, Status = "ASSIGNED" });
        mentorRepo.Assignments.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = mentorRepo.AddTeam(semester), MentorId = mentor1, Status = "ENDED", EndedAt = DateTimeOffset.UtcNow }); // nhóm cũ đã kết thúc

        mentorRepo.Assignments.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = mentorRepo.AddTeam(semester), MentorId = mentor2, Status = "ASSIGNED" });

        // Act: Đếm cho cả 3 mentor
        var counts = await service.GetMentorTeamCountsAsync([mentor1, mentor2, mentor3], null, CancellationToken.None);

        // Assert:
        Assert.Equal(3, counts.Count);
        Assert.Equal(2, counts.Single(c => c.MentorId == mentor1).CurrentTeamCount);
        Assert.Equal(1, counts.Single(c => c.MentorId == mentor2).CurrentTeamCount);
        Assert.Equal(0, counts.Single(c => c.MentorId == mentor3).CurrentTeamCount);
    }

    [Fact]
    public async Task GetMentorTeamCounts_ExcludesDisbandedTeams_WithAndWithoutSemester()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var service = new AdvisorAssignmentService(new StubLecturerRepo(), mentorRepo, teamRepo, new TeamReader(teamRepo, new FakeAcademic()),
            MentorWriterHarness.Create().Writer);

        var mentor = Guid.NewGuid();
        var current = Guid.NewGuid();
        var previous = Guid.NewGuid();
        void Assign(Guid teamId) => mentorRepo.Assignments.Add(new MentorAssignment { Id = Guid.NewGuid(), TeamId = teamId, MentorId = mentor, Status = "ASSIGNED" });
        Assign(mentorRepo.AddTeam(current));
        Assign(mentorRepo.AddTeam(current, "FINALIZED"));
        Assign(mentorRepo.AddTeam(current, "DISBANDED")); // giải tán nhưng dòng gán chưa kết thúc: không tính
        Assign(mentorRepo.AddTeam(previous));
        Assign(mentorRepo.AddTeam(previous, "DISBANDED"));

        var inSemester = await service.GetMentorTeamCountsAsync([mentor], current, CancellationToken.None);
        Assert.Equal(2, Assert.Single(inSemester).CurrentTeamCount);

        var allSemesters = await service.GetMentorTeamCountsAsync([mentor], null, CancellationToken.None);
        Assert.Equal(3, Assert.Single(allSemesters).CurrentTeamCount);
    }

    [Fact]
    public async Task InternalMentorsController_EnforcesAuthorizationPolicies()
    {
        // 1. Không có token -> 401 Unauthorized
        var unauthClient = _api.CreateClient();
        var responseUnauth = await unauthClient.PostAsJsonAsync("/internal/mentors/team-counts", new MentorTeamCountsRequest([Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.Unauthorized, responseUnauth.StatusCode);

        // 2. Token vai trò Student -> 403 Forbidden
        var studentClient = _api.ClientFor(Guid.NewGuid(), Roles.Student);
        var responseStudent = await studentClient.PostAsJsonAsync("/internal/mentors/team-counts", new MentorTeamCountsRequest([Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.Forbidden, responseStudent.StatusCode);

        // 3. Token vai trò Lecturer -> 403 Forbidden
        var lecturerClient = _api.ClientFor(null, Roles.Lecturer);
        var responseLecturer = await lecturerClient.PostAsJsonAsync("/internal/mentors/team-counts", new MentorTeamCountsRequest([Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.Forbidden, responseLecturer.StatusCode);

        // 4. Token vai trò Admin -> 403 Forbidden (chỉ Roles.Service theo policy Policies.Internal)
        var adminClient = _api.ClientFor(null, Roles.Admin);
        var responseAdmin = await adminClient.PostAsJsonAsync("/internal/mentors/team-counts", new MentorTeamCountsRequest([Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.Forbidden, responseAdmin.StatusCode);

        // 5. Token vai trò Service -> 200 OK
        var serviceClient = _api.ClientFor(null, Roles.Service);
        var responseService = await serviceClient.PostAsJsonAsync("/internal/mentors/team-counts", new MentorTeamCountsRequest([]));
        Assert.Equal(HttpStatusCode.OK, responseService.StatusCode);
        var content = await responseService.Content.ReadFromJsonAsync<List<MentorTeamCountResponse>>();
        Assert.NotNull(content);
        Assert.Empty(content);
    }

    [Fact]
    public async Task MentorRegisteredHandler_DeserializesAcademicPayloadAndExecutes()
    {
        var mentorRepo = new InMemoryMentorRepo();
        var teamRepo = new StubTeamRepo();
        var lecturerRepo = new StubLecturerRepo();
        var academic = new FakeAcademic();
        var reader = new TeamReader(teamRepo, academic);
        var harness = MentorWriterHarness.Create();
        var service = new AdvisorAssignmentService(lecturerRepo, mentorRepo, teamRepo, reader, harness.Writer);
        var handler = new MentorRegisteredHandler(service, NullLogger<MentorRegisteredHandler>.Instance);

        var mentorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        mentorRepo.Assignments.Add(new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MentorId = mentorId,
            MentorName = "Lê Văn Tuấn",
            Status = "ASSIGNED",
            Reason = "MENTOR_INTEREST",
            Source = "AUTO"
        });

        // JSON thực tế từ Academic Service (có các trường dư so với Team: domainIds, organization, email, ...)
        var academicJson = $$"""
        {
          "mentorId": "{{mentorId}}",
          "fullName": "Lê Văn Tuấn",
          "email": "tuan@startup.vn",
          "organization": "Startup X",
          "isInternal": false,
          "lecturerId": null,
          "domainIds": [4, 7],
          "maxTeams": 4,
          "isActive": false
        }
        """;

        using var jsonDoc = JsonDocument.Parse(academicJson);
        var envelope = EventEnvelope.Create("MentorRegistered", jsonDoc.RootElement, "academic", "corr-test-123");

        await handler.HandleAsync(envelope, CancellationToken.None);

        // Đã được xử lý gỡ thành công
        var assignment = Assert.Single(mentorRepo.Assignments);
        Assert.Equal("ENDED", assignment.Status);
        Assert.Equal("MENTOR_DEACTIVATED", assignment.EndReason);
        Assert.Single(harness.Outbox());
        Assert.Single(harness.Audits());
    }
}
