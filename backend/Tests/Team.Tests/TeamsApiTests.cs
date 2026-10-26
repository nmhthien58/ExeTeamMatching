using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AuditLog;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// API module Teams (BE-25, BE-26, BE-27) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c> sau
/// <c>docker compose -f deploy/docker-compose.yml up -d postgres</c>. Mỗi test một học kỳ ngẫu nhiên, dọn khi xong.
/// </summary>
public sealed class TeamsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Student_creates_team_with_project_and_becomes_leader()
    {
        if (!Enabled)
        {
            return;
        }

        var me = Academic.Enroll(specializationId: 2);
        var response = await _api.ClientFor(me.Id).PostAsJsonAsync("/api/teams",
            new TeamInput("Nhóm Bình Minh", "EduSpark", "Nền tảng học nhóm", [4, 1], 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var team = (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal(("DRAFT", "EXE101", 1, 5, "LEADER"), (team.Status, team.CourseCode, team.MemberCount, team.OpenSlots, team.MyRelation));
        Assert.Equal(me.Id, team.Leader!.Id);
        Assert.Equal("Chuyên ngành 2", team.Members.Single().Specialization.Name);
        Assert.Equal(["D01", "D04"], team.Domains.Select(d => d.Code)); // domain chính đứng đầu
        Assert.Equal(("EduSpark", "Nền tảng học nhóm", 1), (team.ProjectName, team.IdeaDescription, team.Project!.CurrentVersion));
        Assert.False(team.Requirement.IsValid);

        var events = await _api.EventsAsync(team.Id);
        Assert.Equal(["TeamCreated", "ProjectCreated"], events.Select(e => e.EventType).Order().Reverse());
        Assert.Equal(me.Id, Payload(events.Single(e => e.EventType == "TeamCreated"), "leaderStudentId").GetGuid());
        Assert.Equal("TEAM_CREATED", await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().Where(a => a.TeamId == team.Id).Select(a => a.Action).SingleAsync()));
        Assert.Equal("CREATED", await _api.QueryDbAsync(db => db.Set<ProjectVersion>().Where(v => v.ProjectId == team.ProjectId).Select(v => v.ChangeType).SingleAsync()));
    }

    [Fact]
    public async Task Create_is_refused_outside_round_twice_or_when_not_enrolled()
    {
        if (!Enabled)
        {
            return;
        }

        var me = Academic.Enroll();
        var client = _api.ClientFor(me.Id);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/teams", Input("Nhóm Một"))).StatusCode);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/teams", Input("Nhóm Hai")), 409, "already-in-team");

        var other = Academic.Enroll();
        await AssertProblemAsync(await _api.ClientFor(other.Id).PostAsJsonAsync("/api/teams", Input("Nhóm Một")), 400, "validation");
        await AssertProblemAsync(await _api.ClientFor(other.Id).PostAsJsonAsync("/api/teams", Input("Nhóm Ba", domainIds: [99])), 400, "validation");

        var outsider = Academic.Enroll(enrolled: false);
        await AssertProblemAsync(await _api.ClientFor(outsider.Id).PostAsJsonAsync("/api/teams", Input("Nhóm Bốn")), 403, "not-in-list");

        Academic.OpenCourses.Clear();
        await AssertProblemAsync(await _api.ClientFor(other.Id).PostAsJsonAsync("/api/teams", Input("Nhóm Năm")), 409, "round-closed");
    }

    [Fact]
    public async Task Leader_edits_team_switches_primary_domain_and_project_gets_new_version()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateAsync(leader, "Nhóm Sửa", project: "Đề tài cũ", domainIds: [1, 2]);
        var member = Academic.Enroll();
        await AddMemberAsync(team.Id, member);

        await AssertProblemAsync(await _api.ClientFor(member.Id).PutAsJsonAsync($"/api/teams/{team.Id}", Input("Không được")), 409, "not-leader");

        var response = await _api.ClientFor(leader.Id).PutAsJsonAsync($"/api/teams/{team.Id}",
            new TeamInput("Nhóm Đã Sửa", "Đề tài mới", "Mô tả mới", [2, 3], 2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal(("Nhóm Đã Sửa", "Đề tài mới", 2), (updated.Name, updated.ProjectName, updated.Project!.CurrentVersion));
        Assert.Equal(["D02", "D03"], updated.Domains.Select(d => d.Code));
        var changed = (await _api.EventsAsync(team.Id)).Single(e => e.EventType == "ProjectChanged");
        Assert.Equal(("EDITED", 2), (Payload(changed, "changeType").GetString(), Payload(changed, "version").GetInt32()));

        // Đổi domain chính giữa hai domain cùng giữ lại (uq_team_domains_primary).
        var flipped = await _api.ClientFor(leader.Id).PutAsJsonAsync($"/api/teams/{team.Id}", new TeamInput("Nhóm Đã Sửa", null, null, [2, 3], 3));
        Assert.Equal(HttpStatusCode.OK, flipped.StatusCode);
        Assert.Equal(["D03", "D02"], (await flipped.Content.ReadFromJsonAsync<TeamDetailResponse>())!.Domains.Select(d => d.Code));
        var back = await _api.ClientFor(leader.Id).PutAsJsonAsync($"/api/teams/{team.Id}", new TeamInput("Nhóm Đã Sửa", null, null, [2, 3], 2));
        Assert.True(back.IsSuccessStatusCode, await back.Content.ReadAsStringAsync());
        Assert.Equal(["D02", "D03"], (await back.Content.ReadFromJsonAsync<TeamDetailResponse>())!.Domains.Select(d => d.Code));

        // Admin sửa được cả khi đợt đã khóa; trưởng nhóm thì không.
        Academic.OpenCourses.Clear();
        await AssertProblemAsync(await _api.ClientFor(leader.Id).PutAsJsonAsync($"/api/teams/{team.Id}", Input("Nhóm Khóa")), 409, "round-closed");
        Assert.Equal(HttpStatusCode.OK, (await _api.ClientFor(null, Roles.Admin).PutAsJsonAsync($"/api/teams/{team.Id}", Input("Nhóm Admin Sửa"))).StatusCode);
    }

    [Fact]
    public async Task Detail_mine_and_home_show_relation_and_next_action()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateAsync(leader, "Nhóm Xem");
        var stranger = Academic.Enroll();

        var seen = await _api.ClientFor(stranger.Id).GetFromJsonAsync<TeamDetailResponse>($"/api/teams/{team.Id}");
        Assert.Equal("NONE", seen!.MyRelation);
        Assert.Equal(team.Id, (await _api.ClientFor(leader.Id).GetFromJsonAsync<TeamDetailResponse>("/api/teams/me"))!.Id);
        await AssertProblemAsync(await _api.ClientFor(stranger.Id).GetAsync("/api/teams/me"), 404, "not-found");

        var home = await _api.ClientFor(leader.Id).GetFromJsonAsync<TeamHomeResponse>("/api/teams/me/home");
        Assert.Equal(("INVITE_MEMBERS", "TEAM_FORMATION", true), (home!.NextAction, home.Phase, home.IsLeader));
        var strangerHome = await _api.ClientFor(stranger.Id).GetFromJsonAsync<TeamHomeResponse>("/api/teams/me/home");
        Assert.Equal(("CREATE_OR_JOIN", (TeamCardResponse?)null), (strangerHome!.NextAction, strangerHome.Team));
    }

    [Fact]
    public async Task Search_filters_teams_and_hides_full_teams_from_students()
    {
        if (!Enabled)
        {
            return;
        }

        var a = await CreateAsync(Academic.Enroll(specializationId: 1), "Alpha AI", domainIds: [1]);
        var b = await CreateAsync(Academic.Enroll(specializationId: 1), "Beta Robot", domainIds: [2]);
        var full = await CreateAsync(Academic.Enroll(specializationId: 1), "Gamma Full", domainIds: [1]);
        await AddMemberAsync(b.Id, Academic.Enroll(specializationId: 3));
        for (var i = 0; i < 5; i++)
        {
            await AddMemberAsync(full.Id, Academic.Enroll(specializationId: 2));
        }
        var student = _api.ClientFor(Academic.Enroll().Id);

        var all = await student.GetFromJsonAsync<PagedResponse<TeamCardResponse>>("/api/teams?sort=name");
        Assert.Equal(["Alpha AI", "Beta Robot"], all!.Items.Select(t => t.Name));
        Assert.Equal(["Alpha AI"], (await student.GetFromJsonAsync<PagedResponse<TeamCardResponse>>("/api/teams?domain=D01"))!.Items.Select(t => t.Name));
        Assert.Equal(["Beta Robot"], (await student.GetFromJsonAsync<PagedResponse<TeamCardResponse>>("/api/teams?search=robot"))!.Items.Select(t => t.Name));
        Assert.Equal(["Alpha AI"], (await student.GetFromJsonAsync<PagedResponse<TeamCardResponse>>("/api/teams?missingSpecializationId=3"))!.Items.Select(t => t.Name));
        var paged = await student.GetFromJsonAsync<PagedResponse<TeamCardResponse>>("/api/teams?sort=name&pageSize=1&page=2");
        Assert.Equal(["Beta Robot"], paged!.Items.Select(t => t.Name));
        Assert.Equal((2, 2), (paged.TotalCount, paged.TotalPages));

        var admin = await _api.ClientFor(null, Roles.Admin).GetFromJsonAsync<PagedResponse<TeamCardResponse>>(
            $"/api/teams?semesterId={Academic.Semester.Id}&invalidOnly=true&sort=name");
        Assert.Equal(["Alpha AI", "Beta Robot"], admin!.Items.Select(t => t.Name)); // Gamma đủ 6 người, 2 chuyên ngành
        Assert.Equal(3, (await _api.ClientFor(null, Roles.Admin).GetFromJsonAsync<PagedResponse<TeamCardResponse>>(
            $"/api/teams?semesterId={Academic.Semester.Id}"))!.TotalCount);

        Academic.OpenCourses.Clear();
        await AssertProblemAsync(await student.GetAsync("/api/teams"), 409, "round-closed");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var ids = await db.Set<TeamEntity>().Where(t => t.SemesterId == Academic.Semester.Id).Select(t => t.Id).ToListAsync();
                var projectIds = await db.Set<TeamEntity>().Where(t => ids.Contains(t.Id) && t.ProjectId != null).Select(t => t.ProjectId!.Value).ToListAsync();
                await db.Set<TeamEntity>().Where(t => ids.Contains(t.Id)).ExecuteDeleteAsync();
                await db.Set<Project>().Where(p => projectIds.Contains(p.Id)).ExecuteDeleteAsync();
                await db.Set<BuildingBlocks.Outbox.OutboxMessage>().Where(m => ids.Contains(m.AggregateId)).ExecuteDeleteAsync();
                return 0;
            });
        }
        await _api.DisposeAsync();
    }

    private static TeamInput Input(string name, IReadOnlyList<int>? domainIds = null) => new(name, null, null, domainIds ?? [1], null);

    private async Task<TeamDetailResponse> CreateAsync(StudentSnapshot leader, string name, string? project = null, IReadOnlyList<int>? domainIds = null)
    {
        var response = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput(name, project, null, domainIds ?? [1], null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
    }

    /// <summary>Thêm thẳng vào DB – lời mời / yêu cầu tham gia là Phase 3.</summary>
    private Task AddMemberAsync(Guid teamId, StudentSnapshot s) => _api.QueryDbAsync(async db =>
    {
        db.Add(new TeamMember
        {
            TeamId = teamId, SemesterId = Academic.Semester.Id, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
            SpecializationId = s.SpecializationId, JoinSource = "INVITATION",
        });
        return await db.SaveChangesAsync();
    });

    private static JsonElement Payload(BuildingBlocks.Outbox.OutboxMessage message, string property) =>
        JsonDocument.Parse(message.Payload).RootElement.GetProperty(property).Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.EndsWith("/" + code, problem.RootElement.GetProperty("type").GetString());
    }
}
