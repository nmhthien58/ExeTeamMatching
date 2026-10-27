using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Posts;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Posts;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Bài tuyển thành viên và bài tìm nhóm (BE-31) với team_db thật. Bật bằng <c>EXE_INTEGRATION=1</c>.</summary>
public sealed class PostsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Fact]
    public async Task Leader_opens_updates_and_closes_one_recruitment_post()
    {
        if (!Enabled)
        {
            return;
        }

        var leader = Academic.Enroll();
        var team = await CreateTeamAsync(leader, "Nhóm Tuyển");
        var leaderClient = _api.ClientFor(leader.Id);

        var opened = await PutRecruitmentAsync(leader, team.Id, new RecruitmentPostInput("Cần dev mobile", "Flutter", [2, 3]));
        var updated = await PutRecruitmentAsync(leader, team.Id, new RecruitmentPostInput("Cần dev mobile và BA", null, [3, 4]));
        var detail = await leaderClient.GetFromJsonAsync<TeamDetailResponse>($"/api/teams/{team.Id}");

        Assert.Equal(opened.Id, updated.Id);
        Assert.Equal(("OPEN", (string?)null), (updated.Status, updated.Description));
        Assert.Equal([3, 4], detail!.RecruitmentPost!.SpecializationIds);
        await AssertProblemAsync(await _api.ClientFor(Academic.Enroll().Id).PutAsJsonAsync($"/api/teams/{team.Id}/recruitment-post",
            new RecruitmentPostInput("Chen ngang", null, null)), 409, "not-leader");
        await AssertProblemAsync(await leaderClient.PutAsJsonAsync($"/api/teams/{team.Id}/recruitment-post",
            new RecruitmentPostInput("Sai", null, [99])), 400, "validation");

        Assert.Equal(HttpStatusCode.NoContent, (await leaderClient.DeleteAsync($"/api/teams/{team.Id}/recruitment-post")).StatusCode);
        Assert.Null((await leaderClient.GetFromJsonAsync<TeamDetailResponse>($"/api/teams/{team.Id}"))!.RecruitmentPost);
        await AssertProblemAsync(await leaderClient.DeleteAsync($"/api/teams/{team.Id}/recruitment-post"), 404, "not-found");

        Academic.OpenCourses.Clear();
        await AssertProblemAsync(await leaderClient.PutAsJsonAsync($"/api/teams/{team.Id}/recruitment-post",
            new RecruitmentPostInput("Hết đợt", null, null)), 409, "round-closed");
    }

    [Fact]
    public async Task Seeking_posts_list_only_students_without_team_and_filter_by_domain_and_specialization()
    {
        if (!Enabled)
        {
            return;
        }

        var seeker = Academic.Enroll(specializationId: 2);
        var other = Academic.Enroll(specializationId: 3);
        var leader = Academic.Enroll();
        var client = _api.ClientFor(seeker.Id);

        var mine = await PutSeekingAsync(seeker, new SeekingPostInput("Frontend", "Thích React", [2, 5]));
        var again = await PutSeekingAsync(seeker, new SeekingPostInput("Frontend + UI", null, [5]));
        await PutSeekingAsync(other, new SeekingPostInput("Backend", null, [2]));
        var leaderPost = await PutSeekingAsync(leader, new SeekingPostInput("Đổi ý", null, null));
        await CreateTeamAsync(leader, "Nhóm Đã Có");

        Assert.Equal(mine.Id, again.Id);
        Assert.Equal([5], (await client.GetFromJsonAsync<SeekingPostResponse>("/api/seeking-posts/me"))!.DomainIds);
        var all = await ListAsync(leader, "");
        Assert.Contains(all, p => p.Id == mine.Id && p.Student.StudentCode == seeker.StudentCode);
        Assert.DoesNotContain(all, p => p.Id == leaderPost.Id);
        Assert.Equal([mine.Id], (await ListAsync(leader, "?domain=D05")).Select(p => p.Id));
        Assert.DoesNotContain(await ListAsync(leader, "?specializationId=3"), p => p.Id == mine.Id);
        await AssertProblemAsync(await _api.ClientFor(leader.Id).PutAsJsonAsync("/api/seeking-posts/me",
            new SeekingPostInput("Muộn rồi", null, null)), 409, "already-in-team");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/seeking-posts/me")).StatusCode);
        await AssertProblemAsync(await client.GetAsync("/api/seeking-posts/me"), 404, "not-found");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.Set<SeekingPost>().Where(p => p.SemesterId == Academic.Semester.Id).ExecuteDeleteAsync();
                return await db.Set<TeamEntity>().Where(t => t.SemesterId == Academic.Semester.Id).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }

    private async Task<TeamDetailResponse> CreateTeamAsync(StudentSnapshot leader, string name)
    {
        var response = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput(name, null, null, [1], null));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
    }

    private async Task<RecruitmentPostResponse> PutRecruitmentAsync(StudentSnapshot leader, Guid teamId, RecruitmentPostInput input)
    {
        var response = await _api.ClientFor(leader.Id).PutAsJsonAsync($"/api/teams/{teamId}/recruitment-post", input);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RecruitmentPostResponse>())!;
    }

    private async Task<SeekingPostResponse> PutSeekingAsync(StudentSnapshot student, SeekingPostInput input)
    {
        var response = await _api.ClientFor(student.Id).PutAsJsonAsync("/api/seeking-posts/me", input);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SeekingPostResponse>())!;
    }

    private async Task<IReadOnlyList<SeekingPostResponse>> ListAsync(StudentSnapshot viewer, string query) =>
        (await _api.ClientFor(viewer.Id).GetFromJsonAsync<PagedResponse<SeekingPostResponse>>("/api/seeking-posts" + query))!.Items;

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.EndsWith("/" + code, problem.RootElement.GetProperty("type").GetString());
    }
}
