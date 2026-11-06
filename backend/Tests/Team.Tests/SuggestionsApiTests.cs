using System.Net.Http.Json;
using BuildingBlocks.Auth;
using Team.Services.Invitations;
using Team.Services.Matching;
using Team.Services.Suggestions;

namespace Team.Tests;

/// <summary>Gợi ý thành viên S18 và nhóm S07 qua Matching (BE-39) với team_db thật và Matching giả. Bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class SuggestionsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Members_are_enriched_marked_invited_and_drop_students_who_already_joined()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await TestData.CreateTeamAsync(_api, "Nhóm Cần Người", extraMembers: 1);
        var other = await TestData.CreateTeamAsync(_api, "Nhóm Khác", extraMembers: 0);
        var designer = Academic.Enroll(specializationId: 3);
        var marketer = Academic.Enroll(specializationId: 4);
        _api.Matching.Members = new SuggestedMembers([3, 4],
        [
            new SuggestedMember(designer.Id, 3, 0.9, ["Nhóm đang thiếu chuyên ngành S3"], ["Thiết kế UI/UX 5/5"]),
            new SuggestedMember(marketer.Id, 4, 0.7, ["Bù kỹ năng Marketing"], []),
            new SuggestedMember(other.Leader!.Id, 1, 0.5, ["Read model chưa cập nhật"], []),
        ]);
        var leader = _api.ClientFor(team.Leader!.Id);
        await leader.PostAsJsonAsync($"/api/teams/{team.Id}/invitations", new SendInvitationRequest(designer.Id, null));

        var result = (await leader.GetFromJsonAsync<MemberSuggestionsResponse>($"/api/teams/{team.Id}/suggested-members"))!;

        Assert.Equal(["S3", "S4"], result.MissingSpecializations.Select(s => s.Code));
        Assert.Equal([designer.Id, marketer.Id], result.Items.Select(i => i.Student.Id));
        Assert.Equal((designer.StudentCode, true, "Thiết kế UI/UX 5/5"),
            (result.Items[0].Student.StudentCode, result.Items[0].AlreadyInvited, result.Items[0].Skills.Single()));
        Assert.False(result.Items[1].AlreadyInvited);

        await TestData.AssertProblemAsync(await _api.ClientFor(marketer.Id).GetAsync($"/api/teams/{team.Id}/suggested-members"), 403, null);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await _api.ClientFor(null, Roles.Admin).GetAsync($"/api/teams/{team.Id}/suggested-members")).StatusCode);
    }

    [Fact]
    public async Task Teams_for_me_only_during_round_and_only_open_teams()
    {
        if (!Enabled)
        {
            return;
        }

        var open = await TestData.CreateTeamAsync(_api, "Nhóm Còn Chỗ", extraMembers: 2);
        var full = await TestData.CreateTeamAsync(_api, "Nhóm Đầy", extraMembers: 5);
        var student = Academic.Enroll(specializationId: 5);
        _api.Matching.Teams = [new SuggestedTeam(full.Id, 0.95, ["Đủ người"]), new SuggestedTeam(open.Id, 0.8, ["Nhóm cần chuyên ngành của bạn"])];

        var me = _api.ClientFor(student.Id);
        var teams = (await me.GetFromJsonAsync<List<TeamSuggestionResponse>>("/api/teams/suggested-for-me"))!;
        Assert.Equal([open.Id], teams.Select(t => t.Team.Id));
        Assert.Equal(["Nhóm cần chuyên ngành của bạn"], teams[0].Reasons);

        Assert.Empty((await _api.ClientFor(open.Leader!.Id).GetFromJsonAsync<List<TeamSuggestionResponse>>("/api/teams/suggested-for-me"))!);
        await TestData.AssertProblemAsync(await me.GetAsync("/api/teams/suggested-for-me?limit=0"), 400, "validation");

        Academic.OpenCourses.Remove("EXE101");
        await TestData.AssertProblemAsync(await me.GetAsync("/api/teams/suggested-for-me"), 409, "round-closed");
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
}
