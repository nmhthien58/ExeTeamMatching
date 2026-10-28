using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.AutoAssign;
using Team.Services.Matching;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Ghép tự động (BE-49, rule 14) với team_db thật và Matching giả. Bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class AutoAssignApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    private Guid SemesterId => Academic.Semester.Id;

    [Fact]
    public async Task Job_plan_fills_review_team_creates_new_team_and_is_idempotent()
    {
        if (!Enabled)
        {
            return;
        }

        var review = await TestData.CreateTeamAsync(_api, "Nhóm Cần Bù", extraMembers: 1);
        await FinalizeRoundAsync();
        var fillers = new[] { Academic.Enroll(3), Academic.Enroll(4) };
        var newcomers = Enumerable.Range(1, 4).Select(i => Academic.Enroll(i)).ToList();
        var otherCourse = Academic.Enroll(2, course: "EXE201");
        var request = new AutoAssignApplyRequest(SemesterId, "EXE101",
            [new AutoAssignFill(review.Id, fillers.Select(s => s.Id).ToList())],
            [new AutoAssignNewTeam("Auto-FA26-01", newcomers[0].Id, [.. newcomers.Select(s => s.Id), otherCourse.Id])]);

        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync(ResultUrl)).StatusCode);
        var applied = await ApplyAsync(request);

        Assert.Equal([review.Id], applied.FilledTeams.Select(t => t.Id));
        Assert.Equal((TeamStatuses.Finalized, 4), (applied.FilledTeams[0].Status, applied.FilledTeams[0].MemberCount));
        var created = Assert.Single(applied.NewTeams);
        Assert.Equal(("Auto-FA26-01", TeamStatuses.Finalized, 4), (created.Name, created.Status, created.MemberCount));
        Assert.Equal(newcomers[0].Id, created.Leader!.Id);
        Assert.Equal([otherCourse.Id], applied.UnassignedStudents.Select(s => s.Id));

        var createdEvents = await _api.EventsAsync(created.Id);
        var teamCreated = createdEvents.First();
        Assert.Equal("TeamCreated", teamCreated.EventType);
        Assert.True(JsonDocument.Parse(teamCreated.Payload).RootElement.GetProperty("isAutoAssigned").GetBoolean());
        Assert.Equal(3, createdEvents.Count(e => e.EventType == "MemberJoined"));
        Assert.Contains("TeamFinalized", createdEvents.Select(e => e.EventType));
        Assert.All(await MembersAsync(review.Id, created.Id), m => Assert.True(m.JoinSource is "AUTO_ASSIGN" or "CREATOR" or "INVITATION"));

        // Job chạy lại cùng phương án: không thêm trùng, không tạo nhóm mới.
        var again = await ApplyAsync(request);
        Assert.Empty(again.FilledTeams);
        Assert.Empty(again.NewTeams);
        Assert.Equal(1, await _api.QueryDbAsync(db => db.Set<TeamEntity>().CountAsync(t => t.SemesterId == SemesterId && t.IsAutoAssigned)));

        // Matching sau khi đã ghép: chỉ còn sinh viên không xếp được.
        _api.Matching.Plan = new AutoAssignPlan([], [], [Academic.Enroll(6).Id]);
        var result = (await Admin.GetFromJsonAsync<AutoAssignResultResponse>(ResultUrl))!;
        Assert.Equal([review.Id], result.FilledTeams.Select(t => t.Id));
        Assert.Equal([created.Id], result.NewTeams.Select(t => t.Id));
        Assert.Single(result.UnassignedStudents);
    }

    [Fact]
    public async Task Admin_run_applies_matching_plan_and_keeps_invalid_new_team_for_review()
    {
        if (!Enabled)
        {
            return;
        }

        var sameSpecialization = Enumerable.Range(0, 4).Select(_ => Academic.Enroll(2)).ToList();
        var left = Academic.Enroll(5);
        _api.Matching.Plan = new AutoAssignPlan([], [new PlannedTeam("Auto-FA26-01", sameSpecialization[0].Id, sameSpecialization.Select(s => s.Id).ToList())], [left.Id]);
        // Tên gợi ý đã có nhóm dùng → thêm hậu tố.
        await TestData.CreateTeamAsync(_api, "Auto-FA26-01", extraMembers: 0);

        await TestData.AssertProblemAsync(await _api.ClientFor(left.Id).PostAsJsonAsync("/api/auto-assignment/run",
            new AutoAssignRunRequest(SemesterId, "EXE101")), 403, null);
        var response = await Admin.PostAsJsonAsync("/api/auto-assignment/run", new AutoAssignRunRequest(SemesterId, "EXE101"));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<AutoAssignResultResponse>())!;

        var created = Assert.Single(result.NewTeams);
        Assert.Equal(("Auto-FA26-01 (2)", TeamStatuses.NeedsReview), (created.Name, created.Status));
        Assert.Equal([left.Id], result.UnassignedStudents.Select(s => s.Id));
        Assert.Equal(left.StudentCode, result.UnassignedStudents[0].StudentCode);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await TestData.DeleteSemesterAsync(_api, SemesterId);
        }
        await _api.DisposeAsync();
    }

    private HttpClient Admin => _api.ClientFor(null, Roles.Admin);

    private string ResultUrl => $"/api/auto-assignment/result?semesterId={SemesterId}&courseCode=EXE101";

    private async Task FinalizeRoundAsync() =>
        Assert.Equal(HttpStatusCode.OK, (await _api.ClientFor(null, Roles.Service).PostAsJsonAsync("/internal/teams/finalize-round",
            new { roundId = Guid.NewGuid(), semesterId = SemesterId, courseCode = "EXE101" })).StatusCode);

    private async Task<AutoAssignResultResponse> ApplyAsync(AutoAssignApplyRequest request)
    {
        var response = await _api.ClientFor(null, Roles.Service).PostAsJsonAsync("/internal/teams/auto-assign/apply", request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AutoAssignResultResponse>())!;
    }

    private Task<List<TeamMember>> MembersAsync(params Guid[] teamIds) =>
        _api.QueryDbAsync(db => db.Set<TeamMember>().AsNoTracking().Where(m => teamIds.Contains(m.TeamId)).ToListAsync());
}
