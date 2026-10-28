using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Auth;
using ClosedXML.Excel;
using Team.Services.Matching;
using Team.Services.Statistics;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Dashboard, sinh viên chưa có nhóm, nhắc hạn, xuất Excel (BE-50) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class StatisticsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    private HttpClient Admin => _api.ClientFor(null, Roles.Admin);

    [Fact]
    public async Task Overview_counts_students_teams_specializations_and_domains()
    {
        if (!Enabled)
        {
            return;
        }

        await TestData.CreateTeamAsync(_api, "Nhóm Bốn", extraMembers: 3);
        await TestData.CreateTeamAsync(_api, "Nhóm Hai", extraMembers: 1);
        await RoundClosedAsync();
        _api.Matching.Plan = new AutoAssignPlan([], [], [Academic.Enroll(2).Id, Academic.Enroll(3).Id]);

        await TestData.AssertProblemAsync(await _api.ClientFor(Guid.NewGuid()).GetAsync("/api/statistics/overview"), 403, null);
        var overview = (await Admin.GetFromJsonAsync<StatisticsOverviewResponse>($"/api/statistics/overview?courseCode=EXE101"))!;

        Assert.Equal((6, 2, 1, 0), (overview.StudentsWithTeam, overview.StudentsWithoutTeam, overview.TeamsNeedingReview, overview.PendingChangeRequests));
        Assert.Equal(new Dictionary<string, int> { ["2"] = 1, ["4"] = 1 }, overview.TeamsBySize);
        Assert.Equal(new CodeCount("S1", 2), overview.BySpecialization[0]);
        Assert.Equal([new CodeCount("D01", 2)], overview.ByDomain);
    }

    [Fact]
    public async Task Unmatched_list_skips_students_with_team_and_filters_pages()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await TestData.CreateTeamAsync(_api, "Nhóm Có Người", extraMembers: 0);
        var free = new[] { Academic.Enroll(2), Academic.Enroll(2), Academic.Enroll(3) };
        // Read model của Matching chậm: còn liệt kê trưởng nhóm vừa tạo nhóm.
        _api.Matching.Plan = new AutoAssignPlan([], [new PlannedTeam("Auto", free[0].Id, [free[0].Id, free[1].Id])], [free[2].Id, team.Leader!.Id]);

        var all = (await Admin.GetFromJsonAsync<PagedResponse<StudentRef>>("/api/unmatched-students?courseCode=EXE101"))!;
        Assert.Equal(3, all.TotalCount);
        Assert.DoesNotContain(all.Items, s => s.Id == team.Leader.Id);

        var bySpecialization = (await Admin.GetFromJsonAsync<PagedResponse<StudentRef>>("/api/unmatched-students?courseCode=EXE101&specializationId=2&pageSize=1"))!;
        Assert.Equal((2, 2, 1), (bySpecialization.TotalCount, bySpecialization.TotalPages, bySpecialization.Items.Count));
        Assert.Equal("S2", bySpecialization.Items[0].Specialization.Code);

        var search = (await Admin.GetFromJsonAsync<PagedResponse<StudentRef>>($"/api/unmatched-students?courseCode=EXE101&search={free[2].StudentCode}"))!;
        Assert.Equal([free[2].Id], search.Items.Select(s => s.Id));
    }

    [Fact]
    public async Task Deadline_targets_list_students_without_team_and_small_teams()
    {
        if (!Enabled)
        {
            return;
        }

        var small = await TestData.CreateTeamAsync(_api, "Nhóm Nhỏ", extraMembers: 2);
        await TestData.CreateTeamAsync(_api, "Nhóm Đủ", extraMembers: 3);
        var lonely = Academic.Enroll(4);
        _api.Matching.Plan = new AutoAssignPlan([], [], [lonely.Id]);

        await TestData.AssertProblemAsync(await Admin.GetAsync($"/internal/deadline-reminder-targets?semesterId={Academic.Semester.Id}&courseCode=EXE101"), 403, null);
        var targets = (await _api.ClientFor(null, Roles.Service)
            .GetFromJsonAsync<DeadlineReminderTargets>($"/internal/deadline-reminder-targets?semesterId={Academic.Semester.Id}&courseCode=EXE101"))!;
        Assert.Equal([lonely.Id], targets.StudentIdsWithoutTeam);
        Assert.Equal([small.Id], targets.TeamIdsUnderMinSize);
    }

    [Fact]
    public async Task Export_writes_one_row_per_member_with_leader_first()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await TestData.CreateTeamAsync(_api, "Nhóm Xuất File", extraMembers: 3);
        await RoundClosedAsync();

        var response = await Admin.GetAsync($"/api/teams/export?semesterId={Academic.Semester.Id}&courseCode=EXE101&status=FINALIZED");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));

        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet(1);
        Assert.Equal("MSSV", sheet.Cell(1, 6).GetString());
        Assert.Equal(5, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(("Nhóm Xuất File", "Đã chốt", team.Leader!.StudentCode, "Trưởng nhóm"),
            (sheet.Cell(2, 3).GetString(), sheet.Cell(2, 4).GetString(), sheet.Cell(2, 6).GetString(), sheet.Cell(2, 9).GetString()));
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

    private async Task RoundClosedAsync() =>
        Assert.Equal(HttpStatusCode.OK, (await _api.ClientFor(null, Roles.Service).PostAsJsonAsync("/internal/teams/finalize-round",
            new { roundId = Guid.NewGuid(), semesterId = Academic.Semester.Id, courseCode = "EXE101" })).StatusCode);
}
