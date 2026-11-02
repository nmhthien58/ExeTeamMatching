using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.AuditLog;
using Team.Services.Invitations;
using Team.Services.Posts;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Lịch sử nhóm và nhật ký hệ thống (BE-32) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class AuditLogApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Team_history_is_newest_first_with_summaries_and_only_for_team_members_or_admin()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var invitee = Academic.Enroll(specializationId: 2);
        var team = await CreateTeamAsync(leader, "Nhóm Nhật Ký");
        var leaderClient = _api.ClientFor(leader.Id);
        Assert.Equal(HttpStatusCode.OK, (await leaderClient.PutAsJsonAsync($"/api/teams/{team.Id}/recruitment-post",
            new RecruitmentPostInput("Cần BA", null, null))).StatusCode);
        var sent = await leaderClient.PostAsJsonAsync($"/api/teams/{team.Id}/invitations", new SendInvitationRequest(invitee.Id, null));
        var invitation = (await sent.Content.ReadFromJsonAsync<InvitationResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await _api.ClientFor(invitee.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null)).StatusCode);

        var history = (await _api.ClientFor(invitee.Id).GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/teams/{team.Id}/audit-logs"))!;
        Assert.Equal(["MEMBER_JOINED", "INVITATION_SENT", "RECRUITMENT_POST_OPENED", "TEAM_CREATED"], history.Items.Select(e => e.Action));
        Assert.Equal([$"{invitee.StudentCode} vào nhóm", $"Mời {invitee.StudentCode} vào nhóm", "Đăng bài tuyển thành viên", "Tạo nhóm Nhóm Nhật Ký"],
            history.Items.Select(e => e.Summary));
        Assert.All(history.Items, e => Assert.Equal(("Student", "Người thử"), (e.ActorRole, e.ActorName)));

        var page = (await leaderClient.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/teams/{team.Id}/audit-logs?page=2&pageSize=3"))!;
        Assert.Equal((4, 2, "TEAM_CREATED"), (page.TotalCount, page.TotalPages, page.Items.Single().Action));
        await AssertProblemAsync(await _api.ClientFor(Academic.Enroll().Id).GetAsync($"/api/teams/{team.Id}/audit-logs"), 403, "forbidden");
        await AssertProblemAsync(await leaderClient.GetAsync($"/api/teams/{Guid.NewGuid()}/audit-logs"), 404, "not-found");
    }

    [Fact]
    public async Task Admin_searches_system_log_by_team_and_time_range()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync(Academic.Enroll(), "Nhóm Admin Xem");
        var admin = _api.ClientFor(null, Roles.Admin);
        var since = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        var before = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        var byTeam = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}&from={since}"))!;
        var old = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}&to={before}"))!;
        var adminHistory = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/teams/{team.Id}/audit-logs"))!;

        Assert.Equal("TEAM_CREATED", Assert.Single(byTeam.Items).Action);
        Assert.Equal(0, old.TotalCount);
        Assert.Single(adminHistory.Items);
        await AssertProblemAsync(await admin.GetAsync($"/api/audit-logs?from={since}&to={before}"), 400, "validation");
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientFor(Academic.Enroll().Id).GetAsync("/api/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Admin_filters_system_log_by_actor_name_and_old_rows_fall_back_to_role()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await CreateTeamAsync(Academic.Enroll(), "Nhóm Lọc Người");
        var admin = _api.ClientFor(null, Roles.Admin);

        var found = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}&actorName={Uri.EscapeDataString("người THỬ")}"))!;
        var none = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}&actorName=khong-co"))!;
        var wildcard = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}&actorName={Uri.EscapeDataString("%")}"))!;

        Assert.Equal("Người thử", Assert.Single(found.Items).ActorName);
        Assert.Equal(0, none.TotalCount);
        Assert.Equal(0, wildcard.TotalCount);

        // Log trước khi có cột actor_name vẫn hiện theo vai trò.
        await _api.QueryDbAsync(db =>
        {
            db.Add(new Team.Repositories.AuditLog.AuditLogEntry { TeamId = team.Id, EntityType = "Team", EntityId = team.Id, Action = "TEAM_UPDATED", ActorRole = Roles.Admin });
            return db.SaveChangesAsync();
        });
        var all = (await admin.GetFromJsonAsync<PagedResponse<AuditLogResponse>>($"/api/audit-logs?teamId={team.Id}"))!;
        Assert.Equal(["Bộ môn", "Người thử"], all.Items.Select(e => e.ActorName));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            // audit_logs chỉ được thêm (trigger trg_audit_logs_append_only) nên để lại; test luôn lọc theo teamId mới tạo.
            await _api.QueryDbAsync(db => db.Set<TeamEntity>().Where(t => t.SemesterId == Academic.Semester.Id).ExecuteDeleteAsync());
        }
        await _api.DisposeAsync();
    }

    private async Task<TeamDetailResponse> CreateTeamAsync(StudentSnapshot leader, string name)
    {
        var response = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput(name, null, null, [1], null));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.EndsWith("/" + code, problem.RootElement.GetProperty("type").GetString());
    }
}
