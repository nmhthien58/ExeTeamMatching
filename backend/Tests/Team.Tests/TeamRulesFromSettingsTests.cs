using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Team.Services.Academic;
using Team.Services.Finalization;
using Team.Services.Invitations;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>BE-90, BE-89: quy tắc lập nhóm lấy theo cấu hình W05 thay vì hằng số.</summary>
public sealed class TeamRulesFromSettingsTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Theory]
    [InlineData(500, 0.2, 100)]
    [InlineData(23, 0.2, 4)]
    [InlineData(4, 0.2, 1)]
    public void Max_teams_is_floor_of_students_times_ratio_and_at_least_one(int students, double ratio, int expected) =>
        Assert.Equal(expected, (TeamRuleSet.Default with { MaxTeamRatio = (decimal)ratio }).MaxTeams(students));

    [Fact]
    public void No_limit_when_the_course_size_is_unknown() => Assert.Null(TeamRuleSet.Default.MaxTeams(0));

    [Fact]
    public void Validity_and_status_follow_the_given_rules()
    {
        var rules = new TeamRuleSet(3, 5, 3, 0.2m);

        Assert.True(TeamRules.IsValid(3, 3, rules));
        Assert.False(TeamRules.IsValid(4, 2, rules));
        Assert.False(TeamRules.IsValid(6, 3, rules));
        Assert.Equal(TeamStatuses.Active, TeamRules.StatusAfterMemberChange(TeamStatuses.Draft, 3, rules));
        Assert.Equal(TeamStatuses.Draft, TeamRules.StatusAfterMemberChange(TeamStatuses.Draft, 3));
    }

    [Fact]
    public void Next_action_asks_for_the_configured_specializations()
    {
        var input = new NextActionInput(true, true, Phases.TeamFormation, TeamStatuses.Active, 4, 2, true, true, false, false);

        Assert.Equal("NONE", TeamRules.NextAction(input));
        Assert.Equal("ADD_SPECIALIZATION", TeamRules.NextAction(input with { Rules = new TeamRuleSet(4, 6, 3, 0.2m) }));
    }

    [Fact]
    public async Task Client_reads_settings_once_then_uses_the_cache()
    {
        var handler = new SettingsHandler("""{"minTeamSize":3,"maxTeamSize":5,"minSpecializations":3,"maxTeamRatio":0.5}""");
        var client = new AcademicClient(new HttpClient(handler) { BaseAddress = new Uri("http://academic") }, new MemoryCache(new MemoryCacheOptions()));

        Assert.Equal(new TeamRuleSet(3, 5, 3, 0.5m), await client.GetTeamRulesAsync(CancellationToken.None));
        await client.GetTeamRulesAsync(CancellationToken.None);

        Assert.Equal(1, handler.Calls);
        Assert.Equal("/api/rule-settings", handler.Path);
    }

    [Fact]
    public async Task Client_falls_back_to_last_known_then_default_when_academic_fails()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var failing = new AcademicClient(new HttpClient(new SettingsHandler(null)) { BaseAddress = new Uri("http://academic") }, cache);
        Assert.Equal(TeamRuleSet.Default, await failing.GetTeamRulesAsync(CancellationToken.None));

        var working = new AcademicClient(new HttpClient(new SettingsHandler("""{"minTeamSize":3}""")) { BaseAddress = new Uri("http://academic") }, cache);
        await working.GetTeamRulesAsync(CancellationToken.None);
        cache.Remove("rule-settings");

        Assert.Equal(3, (await failing.GetTeamRulesAsync(CancellationToken.None)).MinSize);
    }

    [Fact]
    public async Task Team_with_two_specializations_needs_review_after_raising_the_minimum_to_three()
    {
        if (!Enabled)
        {
            return;
        }

        var team = await TestData.CreateTeamAsync(_api, "Nhóm Hai Ngành", extraMembers: 3, specializationId: 2);
        Academic.Rules = Academic.Rules with { MinSpecializations = 3 };
        _api.Matching.Rules = Academic.Rules;

        var response = await _api.ClientFor(null, BuildingBlocks.Auth.Roles.Service).PostAsJsonAsync("/internal/teams/finalize-round",
            new FinalizeRoundRequest(Guid.NewGuid(), Academic.Semester.Id, "EXE101"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new FinalizeRoundResult(0, 1), await response.Content.ReadFromJsonAsync<FinalizeRoundResult>());
        var detail = await _api.ClientFor(null, BuildingBlocks.Auth.Roles.Admin).GetFromJsonAsync<TeamDetailResponse>($"/api/teams/{team.Id}");
        Assert.Equal(TeamStatuses.NeedsReview, detail!.Status);
        Assert.Equal(3, detail.Requirement!.MinSpecializations);
    }

    [Fact]
    public async Task Team_is_full_at_the_configured_size()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.Rules = Academic.Rules with { MinSize = 2, MaxSize = 2 };
        var team = await TestData.CreateTeamAsync(_api, "Nhóm Hai Người", extraMembers: 1);
        var leader = team.Leader!.Id;
        var outsider = Academic.Enroll();

        var response = await _api.ClientFor(leader).PostAsJsonAsync($"/api/teams/{team.Id}/invitations", new SendInvitationRequest(outsider.Id, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("team-full", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Creating_a_team_past_the_course_limit_is_refused()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.Rules = Academic.Rules with { MaxTeamRatio = 0.2m };
        var students = Enumerable.Range(0, 5).Select(_ => Academic.Enroll()).ToList();
        Assert.Equal(HttpStatusCode.Created, (await _api.ClientFor(students[0].Id).PostAsJsonAsync("/api/teams", new TeamInput("Nhóm Một", null, null, [1], null))).StatusCode);

        var response = await _api.ClientFor(students[1].Id).PostAsJsonAsync("/api/teams", new TeamInput("Nhóm Hai", null, null, [1], null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("team-limit-reached", await response.Content.ReadAsStringAsync());
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

    private sealed class SettingsHandler(string? body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            (Calls, Path) = (Calls + 1, request.RequestUri!.AbsolutePath);
            return body is null
                ? throw new HttpRequestException("Academic không trả lời")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
