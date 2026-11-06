using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AuditLog;
using Team.Repositories.BaselineImport;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.ChangeRequests;
using Team.Services.Teams;

namespace Team.Tests;

public sealed class ChangeRequestsTests
{
    private sealed class InMemoryChangeRequestRepo : IChangeRequestRepository
    {
        public List<TeamMember> Members { get; } = [];
        public List<TeamBaselineMember> BaselineMembers { get; } = [];
        public List<ChangeRequestItem> Items { get; } = [];
        public List<ChangeRequest> Requests { get; } = [];
        public List<Project> Projects { get; } = [];
        public List<ProjectVersion> ProjectVersions { get; } = [];
        public List<LecturerAssignment> Assignments { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<List<TeamMember>> ListMembersAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Members.Where(m => m.TeamId == teamId).OrderBy(m => m.JoinedAt).ToList());

        public Task<List<TeamBaselineMember>> ListBaselineMembersAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(BaselineMembers.Where(m => m.TeamId == teamId).OrderBy(m => m.StudentCode).ToList());

        public Task<List<ChangeRequestItem>> ListPendingItemsAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Items.Where(i => i.TeamId == teamId && i.ChangeRequestId == null).OrderBy(i => i.CreatedAt).ToList());

        public Task<List<ChangeRequestItem>> ListItemsAsync(Guid requestId, CancellationToken ct) =>
            Task.FromResult(Items.Where(i => i.ChangeRequestId == requestId).OrderBy(i => i.CreatedAt).ToList());

        public Task<List<ChangeRequestItem>> ListApprovedItemsBeforeAsync(Guid teamId, DateTimeOffset createdAt, CancellationToken ct)
        {
            var approvedIds = Requests.Where(r => r.TeamId == teamId && r.Status == "APPROVED" && r.CreatedAt < createdAt)
                .Select(r => r.Id).ToHashSet();
            return Task.FromResult(Items.Where(i => i.ChangeRequestId != null && approvedIds.Contains(i.ChangeRequestId.Value)).ToList());
        }

        public Task<ChangeRequestItem?> GetPendingItemAsync(Guid teamId, Guid itemId, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId && i.TeamId == teamId && i.ChangeRequestId == null));

        public Task<ChangeRequestItem?> GetPendingProjectChangeAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(i => i.TeamId == teamId && i.ChangeRequestId == null && i.Action == "PROJECT_CHANGE"));

        public Task<ChangeRequest?> GetAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

        public Task<bool> HasPendingAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Requests.Any(r => r.TeamId == teamId && r.Status == "PENDING"));

        public Task<List<ChangeRequest>> ListByTeamAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Requests.Where(r => r.TeamId == teamId).OrderByDescending(r => r.CreatedAt).ToList());

        public Task<(List<ChangeRequest> Items, int TotalCount)> SearchAsync(string? status, Guid? roundId, int page, int pageSize, CancellationToken ct)
        {
            var q = Requests.AsEnumerable();
            if (status != null) q = q.Where(r => r.Status == status);
            if (roundId != null) q = q.Where(r => r.RoundId == roundId);
            var list = q.OrderByDescending(r => r.CreatedAt).ToList();
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((paged, list.Count));
        }

        public Task<Project?> GetProjectAsync(Guid? projectId, CancellationToken ct) =>
            Task.FromResult(projectId == null ? null : Projects.FirstOrDefault(p => p.Id == projectId));

        public Task<ProjectVersion?> GetProjectVersionForRequestAsync(Guid requestId, CancellationToken ct) =>
            Task.FromResult(ProjectVersions.FirstOrDefault(v => v.ChangeRequestId == requestId));

        public Task<ProjectVersion?> GetProjectVersionAsync(Guid projectId, short version, CancellationToken ct) =>
            Task.FromResult(ProjectVersions.FirstOrDefault(v => v.ProjectId == projectId && v.Version == version));

        public Task<LecturerAssignment?> GetAssignedLecturerAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Assignments.FirstOrDefault(a => a.TeamId == teamId && a.Status == "ASSIGNED"));

        public void Add(object entity)
        {
            switch (entity)
            {
                case ChangeRequest r: Requests.Add(r); break;
                case ChangeRequestItem i: Items.Add(i); break;
                case TeamMember m: Members.Add(m); break;
                case ProjectVersion v: ProjectVersions.Add(v); break;
            }
        }

        public void Remove(object entity)
        {
            switch (entity)
            {
                case ChangeRequestItem i: Items.Remove(i); break;
                case TeamMember m: Members.Remove(m); break;
            }
        }

        public void AddEvent(string eventType, Guid requestId, object payload)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Topic = BuildingBlocks.Messaging.Topics.ChangeRequest,
                EventType = eventType,
                AggregateType = "ChangeRequest",
                AggregateId = requestId,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                OccurredAt = DateTimeOffset.UtcNow,
            });
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryTeamRepo : ITeamRepository
    {
        public List<TeamEntity> Teams { get; } = [];
        public List<TeamMember> Members { get; } = [];
        public List<Project> Projects { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(Teams.FirstOrDefault(t => t.Id == teamId));

        public Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct)
        {
            var team = Teams.FirstOrDefault(t => t.Id == teamId);
            if (team == null) return Task.FromResult<TeamGraph?>(null);
            var members = Members.Where(m => m.TeamId == teamId).ToList();
            var project = Projects.FirstOrDefault(p => p.Id == team.ProjectId);
            return Task.FromResult<TeamGraph?>(new TeamGraph(team, members, [], project, null, null));
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
                    res.Add(new TeamGraph(team, members, [], project, null, null));
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
            Task.FromResult(Teams.Any(t => t.SemesterId == semesterId && t.CourseCode == courseCode && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && t.Id != exceptTeamId));

        public Task<List<TeamDomain>> ListDomainsAsync(Guid teamId, CancellationToken ct) =>
            Task.FromResult(new List<TeamDomain>());

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

        public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Topic = BuildingBlocks.Messaging.Topics.Team,
                EventType = eventType,
                AggregateType = "Team",
                AggregateId = teamId,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload),
                OccurredAt = DateTimeOffset.UtcNow,
            });
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (ChangeRequestService Service, InMemoryChangeRequestRepo ChangeRepo, InMemoryTeamRepo TeamRepo, FakeAcademic Academic, TeamEntity Team, Guid LeaderId) CreateTestSetup()
    {
        var academic = new FakeAcademic();
        var changeRepo = new InMemoryChangeRequestRepo();
        var teamRepo = new InMemoryTeamRepo();

        var teamId = Guid.NewGuid();
        var semesterId = academic.Semester.Id;
        var leaderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "EXE Project Baseline",
            Description = "Mô tả dự án gốc",
            OriginCourseCode = "EXE101",
            CurrentVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        teamRepo.Projects.Add(project);
        changeRepo.Projects.Add(project);

        var team = new TeamEntity
        {
            Id = teamId,
            SemesterId = semesterId,
            CourseCode = "EXE201",
            Name = "Nhóm EXE201 Kế Thừa",
            ProjectId = projectId,
            Status = TeamStatuses.Active,
            MemberCount = 5,
            IsInherited = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        teamRepo.Teams.Add(team);

        // Tạo 5 thành viên mốc và hiện tại
        var leader = new TeamMember
        {
            Id = Guid.NewGuid(), TeamId = teamId, SemesterId = semesterId, StudentId = leaderId,
            StudentCode = "SE160001", FullName = "Trưởng Nhóm", SpecializationId = 1,
            Role = MemberRoles.Leader, JoinSource = "INHERITED", JoinedAt = DateTimeOffset.UtcNow.AddDays(-10),
        };
        teamRepo.Members.Add(leader);
        changeRepo.Members.Add(leader);
        changeRepo.BaselineMembers.Add(new TeamBaselineMember
        {
            TeamId = teamId, StudentId = leaderId, StudentCode = leader.StudentCode, FullName = leader.FullName,
            SpecializationId = leader.SpecializationId, Role = leader.Role, ImportedAt = leader.JoinedAt,
        });

        for (int i = 2; i <= 5; i++)
        {
            var memberId = Guid.NewGuid();
            var m = new TeamMember
            {
                Id = Guid.NewGuid(), TeamId = teamId, SemesterId = semesterId, StudentId = memberId,
                StudentCode = $"SE16000{i}", FullName = $"Thành viên {i}", SpecializationId = i % 2 == 0 ? 1 : 2,
                Role = MemberRoles.Member, JoinSource = "INHERITED", JoinedAt = DateTimeOffset.UtcNow.AddDays(-10),
            };
            teamRepo.Members.Add(m);
            changeRepo.Members.Add(m);
            changeRepo.BaselineMembers.Add(new TeamBaselineMember
            {
                TeamId = teamId, StudentId = memberId, StudentCode = m.StudentCode, FullName = m.FullName,
                SpecializationId = m.SpecializationId, Role = m.Role, ImportedAt = m.JoinedAt,
            });
        }

        var db = new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
            .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options);
        var audit = new AuditLogWriter(db);
        var reader = new TeamReader(teamRepo, academic);
        var service = new ChangeRequestService(changeRepo, teamRepo, academic, reader, audit, TimeProvider.System);

        return (service, changeRepo, teamRepo, academic, team, leaderId);
    }

    private static ClaimsPrincipal UserClaims(Guid? studentId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, Guid.NewGuid().ToString()),
            new(ClaimNames.Name, "Người thử"),
        };
        if (studentId is { } sid)
        {
            claims.Add(new(ClaimNames.StudentId, sid.ToString()));
        }
        foreach (var role in roles.Length == 0 ? [Roles.Student] : roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task GetPendingChanges_Returns_Baseline_PendingItems_And_Projected_Counts()
    {
        var (service, changeRepo, _, _, team, leaderId) = CreateTestSetup();

        // Thêm 1 item ADD và 1 item REMOVE đang chờ
        var newStudentId = Guid.NewGuid();
        changeRepo.Items.Add(new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, Action = "ADD", Source = "INVITATION",
            StudentId = newStudentId, StudentCode = "SE170001", FullName = "Người mới",
            SpecializationId = 3, CreatedAt = DateTimeOffset.UtcNow,
        });

        var pending = await service.GetPendingChangesAsync(team.Id, UserClaims(leaderId), CancellationToken.None);

        Assert.Equal(5, pending.BaselineMembers.Count);
        Assert.Single(pending.PendingItems);
        Assert.Equal(6, pending.WouldHaveMemberCount);
        Assert.Equal(3, pending.WouldHaveSpecializationCount);
        Assert.Equal("EXE Project Baseline", pending.CurrentProject?.Name);
    }

    [Fact]
    public async Task GetPendingChanges_Throws_Forbidden_When_User_Not_Member_And_Not_Admin()
    {
        var (service, _, _, _, team, _) = CreateTestSetup();
        var outsiderId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetPendingChangesAsync(team.Id, UserClaims(outsiderId), CancellationToken.None));
    }

    [Fact]
    public async Task SetProjectChange_Creates_And_Updates_Pending_Item()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();

        // Cần có đợt đổi nhóm mở
        var roundId = Guid.NewGuid();
        academic.OpenRound = new RoundDto(roundId, team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN", "Đợt đổi nhóm");

        // Gọi lần 1: tạo mới
        var result1 = await service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("EduNext Pivot", "Mô tả mới"),
            UserClaims(leaderId), CancellationToken.None);

        Assert.Equal("PROJECT_CHANGE", result1.Action);
        Assert.Equal("EduNext Pivot", result1.NewProjectName);
        Assert.Single(changeRepo.Items);

        // Gọi lần 2: cập nhật đè
        var result2 = await service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("EduNext Pivot V2", "Mô tả V2"),
            UserClaims(leaderId), CancellationToken.None);

        Assert.Equal("EduNext Pivot V2", result2.NewProjectName);
        Assert.Single(changeRepo.Items); // Không tạo dòng thứ 2
    }

    [Fact]
    public async Task SetProjectChange_Throws_When_Not_Leader()
    {
        var (service, _, _, academic, team, _) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        var regularMemberId = Guid.NewGuid(); // không phải leader
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("New Name", "Desc"), UserClaims(regularMemberId), CancellationToken.None));
        Assert.Equal("not-leader", ex.Code);
    }

    [Fact]
    public async Task SetProjectChange_Throws_When_Team_Not_Inherited()
    {
        var (service, _, _, academic, team, leaderId) = CreateTestSetup();
        team.IsInherited = false;
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("New Name", "Desc"), UserClaims(leaderId), CancellationToken.None));
        Assert.Equal("change-request-not-allowed", ex.Code);
    }

    [Fact]
    public async Task SetProjectChange_Throws_When_Round_Closed()
    {
        var (service, _, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = null; // Đợt đóng

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("New Name", "Desc"), UserClaims(leaderId), CancellationToken.None));
        Assert.Equal("round-closed", ex.Code);
    }

    [Fact]
    public async Task DeleteProjectChange_Removes_Item()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        await service.SetProjectChangeAsync(team.Id, new ProjectChangeInput("Pivot", null), UserClaims(leaderId), CancellationToken.None);
        Assert.Single(changeRepo.Items);

        await service.DeleteProjectChangeAsync(team.Id, UserClaims(leaderId), CancellationToken.None);
        Assert.Empty(changeRepo.Items);
    }

    [Fact]
    public async Task SubmitAsync_Valid_Creates_Pending_Request_Calculates_Percent_And_Emits_Event()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN", "Đợt đổi nhóm");

        // 1 item ADD và 1 item REMOVE
        var newStudentId = Guid.NewGuid();
        var itemAdd = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, Action = "ADD", Source = "INVITATION",
            StudentId = newStudentId, StudentCode = "SE170001", FullName = "Người mới", SpecializationId = 2,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var removeMemberId = changeRepo.Members.Last().StudentId;
        var itemRemove = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, Action = "REMOVE", Source = "LEAVE",
            StudentId = removeMemberId, StudentCode = "SE160005", FullName = "Thành viên 5", SpecializationId = 2,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        changeRepo.Items.AddRange([itemAdd, itemRemove]);

        var result = await service.SubmitAsync(team.Id,
            new ChangeRequestInput([itemAdd.Id, itemRemove.Id], "Đổi thành viên phù hợp với đề tài"),
            UserClaims(leaderId), CancellationToken.None);

        Assert.Equal("PENDING", result.Status);
        Assert.Equal(2, result.ChangedMemberCount);
        // baseline = 5 người -> 2 / 5 = 40.00%
        Assert.Equal(40.00m, result.ChangedPercent);

        // Items đã được gắn ChangeRequestId
        Assert.All(changeRepo.Items, i => Assert.Equal(result.Id, i.ChangeRequestId));

        // Event Outbox
        var evt = Assert.Single(changeRepo.OutboxMessages);
        Assert.Equal("ChangeRequestSubmitted", evt.EventType);
    }

    [Fact]
    public async Task SubmitAsync_Throws_When_Projected_Members_Under_MinSize()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        // Xóa 2 thành viên khỏi nhóm 5 người -> còn 3 người (< 4)
        var mem1 = changeRepo.Members[3].StudentId;
        var mem2 = changeRepo.Members[4].StudentId;
        var item1 = new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = team.Id, Action = "REMOVE", Source = "REMOVE", StudentId = mem1 };
        var item2 = new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = team.Id, Action = "REMOVE", Source = "REMOVE", StudentId = mem2 };
        changeRepo.Items.AddRange([item1, item2]);

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.SubmitAsync(team.Id, new ChangeRequestInput([item1.Id, item2.Id], "Lý do hợp lệ trên 10 ký tự"),
                UserClaims(leaderId), CancellationToken.None));
        Assert.Contains("ít nhất 4 thành viên", ex.Errors["itemIds"].First());
    }

    [Fact]
    public async Task SubmitAsync_Throws_When_Projected_Members_Over_MaxSize()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        // Nhóm ban đầu 5 người, thêm 2 người -> 7 người (> 6)
        var item1 = new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = team.Id, Action = "ADD", Source = "INVITATION", StudentId = Guid.NewGuid(), SpecializationId = 1, StudentCode = "SE180010", FullName = "SV mới 1" };
        var item2 = new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = team.Id, Action = "ADD", Source = "INVITATION", StudentId = Guid.NewGuid(), SpecializationId = 2, StudentCode = "SE180011", FullName = "SV mới 2" };
        changeRepo.Items.AddRange([item1, item2]);

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.SubmitAsync(team.Id, new ChangeRequestInput([item1.Id, item2.Id], "Lý do hợp lệ trên 10 ký tự"),
                UserClaims(leaderId), CancellationToken.None));
        Assert.Contains("không được quá 6 thành viên", ex.Errors["itemIds"].First());
    }

    [Fact]
    public async Task SubmitAsync_Throws_When_Pending_Request_Already_Exists()
    {
        var (service, changeRepo, _, academic, team, leaderId) = CreateTestSetup();
        academic.OpenRound = new RoundDto(Guid.NewGuid(), team.SemesterId, "EXE201", RoundTypes.ChangeRequest, DateTimeOffset.UtcNow.AddDays(7), "OPEN");

        changeRepo.Requests.Add(new ChangeRequest
        {
            Id = Guid.NewGuid(), TeamId = team.Id, Status = "PENDING", Reason = "Yêu cầu cũ",
        });

        var item = new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = team.Id, Action = "ADD", Source = "INVITATION", StudentId = Guid.NewGuid(), SpecializationId = 1, StudentCode = "SE180001", FullName = "SV mới" };
        changeRepo.Items.Add(item);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SubmitAsync(team.Id, new ChangeRequestInput([item.Id], "Lý do hợp lệ trên 10 ký tự"),
                UserClaims(leaderId), CancellationToken.None));
        Assert.Equal("pending-exists", ex.Code);
    }

    [Fact]
    public async Task SearchAsync_Filters_By_Status_And_Round()
    {
        var (service, changeRepo, _, _, team, _) = CreateTestSetup();
        var round1 = Guid.NewGuid();
        var round2 = Guid.NewGuid();

        changeRepo.Requests.Add(new ChangeRequest { Id = Guid.NewGuid(), TeamId = team.Id, RoundId = round1, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) });
        changeRepo.Requests.Add(new ChangeRequest { Id = Guid.NewGuid(), TeamId = team.Id, RoundId = round1, Status = "APPROVED", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) });
        changeRepo.Requests.Add(new ChangeRequest { Id = Guid.NewGuid(), TeamId = team.Id, RoundId = round2, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow });

        var pendingRound1 = await service.SearchAsync(new ChangeRequestQuery { Status = "PENDING", RoundId = round1 }, CancellationToken.None);
        Assert.Single(pendingRound1.Items);
        Assert.Equal("PENDING", pendingRound1.Items.First().Status);
        Assert.Equal(round1, pendingRound1.Items.First().RoundId);
    }

    [Fact]
    public async Task ApproveAsync_Applies_Add_Remove_And_ProjectChange_Pivot()
    {
        var (service, changeRepo, teamRepo, _, team, _) = CreateTestSetup();
        var requestId = Guid.NewGuid();
        var request = new ChangeRequest
        {
            Id = requestId, TeamId = team.Id, Status = "PENDING", Reason = "Thay đổi đề tài và thành viên",
            ChangedMemberCount = 2, ChangedPercent = 40.00m, CreatedAt = DateTimeOffset.UtcNow,
        };
        changeRepo.Requests.Add(request);

        // 1 ADD, 1 REMOVE, 1 PROJECT_CHANGE
        var newStudentId = Guid.NewGuid();
        var itemAdd = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, ChangeRequestId = requestId, Action = "ADD", Source = "INVITATION",
            StudentId = newStudentId, StudentCode = "SE190001", FullName = "Thành viên mới", SpecializationId = 2,
        };
        var removeStudentId = changeRepo.Members.Last().StudentId;
        var itemRemove = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, ChangeRequestId = requestId, Action = "REMOVE", Source = "LEAVE",
            StudentId = removeStudentId, StudentCode = "SE160005", FullName = "Thành viên 5", SpecializationId = 2,
        };
        var itemPivot = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, ChangeRequestId = requestId, Action = "PROJECT_CHANGE", Source = "PROJECT",
            NewProjectName = "EXE Project Pivot", NewProjectDescription = "Mô tả đề tài mới sau pivot",
        };
        changeRepo.Items.AddRange([itemAdd, itemRemove, itemPivot]);

        var adminUser = UserClaims(null, Roles.Admin);
        var response = await service.ApproveAsync(requestId, new ChangeRequestDecisionInput("Duyệt thay đổi"), adminUser, CancellationToken.None);

        Assert.Equal("APPROVED", response.Status);
        Assert.Equal("Duyệt thay đổi", request.ReviewNote);

        // Kiểm tra thành viên đã được áp dụng
        Assert.Contains(changeRepo.Members, m => m.StudentId == newStudentId && m.JoinSource == "CHANGE_REQUEST");
        Assert.DoesNotContain(changeRepo.Members, m => m.StudentId == removeStudentId);
        Assert.Equal(5, changeRepo.Members.Count); // 5 - 1 + 1 = 5

        // Kiểm tra đề tài đã pivot
        var project = changeRepo.Projects.First(p => p.Id == team.ProjectId);
        Assert.Equal("EXE Project Pivot", project.Name);
        Assert.Equal(2, project.CurrentVersion); // Version tăng lên 2
        Assert.Single(changeRepo.ProjectVersions);
        Assert.Equal("PIVOT", changeRepo.ProjectVersions.First().ChangeType);

        // Kiểm tra Outbox events
        Assert.Contains(changeRepo.OutboxMessages, m => m.EventType == "ChangeRequestApproved");
        Assert.Contains(teamRepo.OutboxMessages, m => m.EventType == "MemberJoined");
        Assert.Contains(teamRepo.OutboxMessages, m => m.EventType == "MemberLeft");
        Assert.Contains(teamRepo.OutboxMessages, m => m.EventType == "ProjectChanged");
    }

    [Fact]
    public async Task RejectAsync_Requires_Note_And_Keeps_Team_Unchanged()
    {
        var (service, changeRepo, _, _, team, _) = CreateTestSetup();
        var requestId = Guid.NewGuid();
        var request = new ChangeRequest
        {
            Id = requestId, TeamId = team.Id, Status = "PENDING", Reason = "Yêu cầu đổi",
            ChangedMemberCount = 1, ChangedPercent = 20.00m, CreatedAt = DateTimeOffset.UtcNow,
        };
        changeRepo.Requests.Add(request);

        var item = new ChangeRequestItem
        {
            Id = Guid.NewGuid(), TeamId = team.Id, ChangeRequestId = requestId, Action = "ADD", Source = "INVITATION",
            StudentId = Guid.NewGuid(), StudentCode = "SE190002", FullName = "SV mới", SpecializationId = 1,
        };
        changeRepo.Items.Add(item);

        var adminUser = UserClaims(null, Roles.Admin);

        // Từ chối không có ghi chú -> ném lỗi
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.RejectAsync(requestId, new ChangeRequestDecisionInput(""), adminUser, CancellationToken.None));
        Assert.Contains("bắt buộc", ex.Errors["note"].First());

        // Từ chối có ghi chú hợp lệ
        var response = await service.RejectAsync(requestId, new ChangeRequestDecisionInput("Lý do chưa thuyết phục"), adminUser, CancellationToken.None);
        Assert.Equal("REJECTED", response.Status);
        Assert.Equal("Lý do chưa thuyết phục", request.ReviewNote);

        // Thành viên không thay đổi
        Assert.Equal(5, changeRepo.Members.Count);
        Assert.DoesNotContain(changeRepo.Members, m => m.StudentId == item.StudentId);

        // Event Outbox
        var evt = Assert.Single(changeRepo.OutboxMessages);
        Assert.Equal("ChangeRequestRejected", evt.EventType);
    }

    private sealed class FakeAcademic : IAcademicClient
    {
        public SemesterDto Semester { get; } = new(Guid.NewGuid(), "FA26", "Fall 2026");
        public RoundDto? OpenRound { get; set; }

        public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => Task.FromResult<SemesterDto?>(Semester);

        public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
            Task.FromResult(OpenRound != null && OpenRound.CourseCode == courseCode && OpenRound.RoundType == roundType ? OpenRound : null);

        public Task<IReadOnlyList<RoundDto>> ListChangeRequestRoundsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RoundDto>>(OpenRound != null ? [OpenRound] : []);

        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

        public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

        public Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>([new(1, "D01", "Domain 1", true)]);

        public Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>([
                new(1, "SE", "Software Engineering", true),
                new(2, "IA", "Information Assurance", true),
                new(3, "GD", "Game Design", true),
            ]);
    }
}
