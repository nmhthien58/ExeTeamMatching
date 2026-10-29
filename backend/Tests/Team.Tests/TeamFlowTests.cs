using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.BaselineImport;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.ChangeRequests;
using Team.Services.Finalization;
using Team.Services.Invitations;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// Luồng xuyên module (BE-58): tạo nhóm → đóng đợt chốt nhóm; nhóm EXE201 kế thừa đổi thành viên qua yêu cầu đổi nhóm → Admin duyệt → chốt.
/// team_db thật, Academic và Matching giả. Bật bằng <c>EXE_INTEGRATION=1</c>.
/// </summary>
public sealed class TeamFlowTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Students_form_team_through_invitations_and_round_close_finalizes_it()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll(specializationId: 1);
        var created = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput("Nhóm Luồng Chính", null, null, [1], null));
        var team = (await created.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        foreach (var specialization in new[] { 2, 3, 3 })
        {
            var student = Academic.Enroll(specialization);
            var invitation = (await (await _api.ClientFor(leader.Id).PostAsJsonAsync($"/api/teams/{team.Id}/invitations",
                new SendInvitationRequest(student.Id, null))).Content.ReadFromJsonAsync<InvitationResponse>())!;
            Assert.Equal(HttpStatusCode.OK, (await _api.ClientFor(student.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null)).StatusCode);
        }
        Assert.Equal((TeamStatuses.Active, 4), await StatusAsync(team.Id));

        // Hết hạn: Discover khóa (rule 18), Job chốt nhóm (rule 13).
        Academic.OpenCourses.Remove("EXE101");
        await TestData.AssertProblemAsync(await _api.ClientFor(Academic.Enroll().Id).PostAsJsonAsync("/api/teams",
            new TeamInput("Nhóm Trễ Hạn", null, null, [1], null)), 409, "round-closed");
        Assert.Equal(new FinalizeRoundResult(1, 0), await FinalizeRoundAsync("EXE101"));
        Assert.Equal((TeamStatuses.Finalized, 4), await StatusAsync(team.Id));
    }

    [Fact]
    public async Task Inherited_team_changes_members_through_approved_change_request_then_is_finalized()
    {
        if (!Enabled)
        {
            return;
        }

        // Nhóm EXE201 kế thừa từ EXE101 (như sau import mốc, BE-43): 4 người, 2 chuyên ngành.
        var baseline = new[] { Academic.Enroll(1, "EXE201"), Academic.Enroll(1, "EXE201"), Academic.Enroll(2, "EXE201"), Academic.Enroll(2, "EXE201") };
        var teamId = await CreateInheritedTeamAsync("Nhóm Kế Thừa", baseline);
        Academic.OpenCourses.Remove("EXE201");
        Academic.OpenChangeRequestCourses.Add("EXE201");
        var leader = _api.ClientFor(baseline[0].Id);

        // Trưởng nhóm bỏ một người và mời người mới: chỉ thành thay đổi chờ duyệt (rule 7).
        var removed = (await (await leader.DeleteAsync($"/api/teams/{teamId}/members/{baseline[3].Id}")).Content.ReadFromJsonAsync<MemberChangeResult>())!;
        Assert.False(removed.AppliedImmediately);
        var newcomer = Academic.Enroll(5, "EXE201");
        var invitation = (await (await leader.PostAsJsonAsync($"/api/teams/{teamId}/invitations", new SendInvitationRequest(newcomer.Id, null)))
            .Content.ReadFromJsonAsync<InvitationResponse>())!;
        await _api.ClientFor(newcomer.Id).PostAsync($"/api/invitations/{invitation.Id}/accept", null);
        Assert.Equal(4, (await MembersAsync(teamId)).Count);

        var pending = (await leader.GetFromJsonAsync<PendingChangesResponse>($"/api/teams/{teamId}/pending-changes"))!;
        Assert.Equal(["ADD", "REMOVE"], pending.PendingItems.Select(i => i.Action).Order());
        var submitted = await leader.PostAsJsonAsync($"/api/teams/{teamId}/change-requests",
            new ChangeRequestInput(pending.PendingItems.Select(i => i.Id).ToList(), "Bạn D chuyển ngành, nhóm cần thêm thiết kế."));
        Assert.True(submitted.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, await submitted.Content.ReadAsStringAsync());
        var request = (await submitted.Content.ReadFromJsonAsync<ChangeRequestResponse>())!;

        var approved = await _api.ClientFor(null, Roles.Admin).PostAsJsonAsync($"/api/change-requests/{request.Id}/approve", new ChangeRequestDecisionInput(null));
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        var members = await MembersAsync(teamId);
        Assert.Contains(members, m => m.StudentId == newcomer.Id && m.JoinSource == "CHANGE_REQUEST");
        Assert.DoesNotContain(members, m => m.StudentId == baseline[3].Id);
        var events = (await _api.EventsAsync(teamId)).Select(e => e.EventType).ToList();
        Assert.Contains("MemberJoined", events);
        Assert.Contains("MemberLeft", events);

        Assert.Equal(new FinalizeRoundResult(1, 0), await FinalizeRoundAsync("EXE201"));
        Assert.Equal((TeamStatuses.Finalized, 4), await StatusAsync(teamId));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await TestData.DeleteSemesterAsync(_api, Academic.Semester.Id);
        }
        await _api.DisposeAsync();
    }

    private async Task<Guid> CreateInheritedTeamAsync(string name, IReadOnlyList<StudentSnapshot> students)
    {
        var teamId = Guid.NewGuid();
        await _api.QueryDbAsync(db =>
        {
            db.Add(new TeamEntity { Id = teamId, SemesterId = Academic.Semester.Id, CourseCode = "EXE201", Name = name, Status = TeamStatuses.Active, IsInherited = true });
            for (var i = 0; i < students.Count; i++)
            {
                var s = students[i];
                var role = i == 0 ? MemberRoles.Leader : MemberRoles.Member;
                db.Add(new TeamMember
                {
                    TeamId = teamId, SemesterId = Academic.Semester.Id, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
                    SpecializationId = s.SpecializationId, Role = role, JoinSource = "INHERITED",
                });
                db.Add(new TeamBaselineMember
                {
                    TeamId = teamId, StudentId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName, SpecializationId = s.SpecializationId, Role = role,
                });
            }
            return db.SaveChangesAsync();
        });
        return teamId;
    }

    private async Task<FinalizeRoundResult> FinalizeRoundAsync(string courseCode)
    {
        var response = await _api.ClientFor(null, Roles.Service).PostAsJsonAsync("/internal/teams/finalize-round",
            new FinalizeRoundRequest(Guid.NewGuid(), Academic.Semester.Id, courseCode));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<FinalizeRoundResult>())!;
    }

    private Task<(string Status, int MemberCount)> StatusAsync(Guid teamId) =>
        _api.QueryDbAsync(db => db.Set<TeamEntity>().Where(t => t.Id == teamId)
            .Select(t => new ValueTuple<string, int>(t.Status, t.MemberCount)).SingleAsync());

    private Task<List<TeamMember>> MembersAsync(Guid teamId) =>
        _api.QueryDbAsync(db => db.Set<TeamMember>().AsNoTracking().Where(m => m.TeamId == teamId).ToListAsync());
}
