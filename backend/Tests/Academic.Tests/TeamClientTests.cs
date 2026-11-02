using System.Net;
using System.Text;
using System.Text.Json;
using Academic.Services.Teams;

namespace Academic.Tests;

/// <summary><see cref="TeamClient"/> (BE-66): body gửi Team và cách đổi lỗi sang <see cref="TeamUnavailableException"/>.</summary>
public sealed class TeamClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request);
        }
    }

    private static TeamClient Client(StubHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("http://team.test") });

    [Fact]
    public async Task Posts_mentor_ids_and_semester_and_reads_counts()
    {
        var mentor = Guid.NewGuid();
        var other = Guid.NewGuid();
        var semester = Guid.NewGuid();
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""[{"mentorId":"{{mentor}}","currentTeamCount":2},{"mentorId":"{{other}}","currentTeamCount":0}]""",
                Encoding.UTF8, "application/json"),
        }));

        var counts = await Client(handler).GetMentorTeamCountsAsync([mentor, other, mentor], semester, CancellationToken.None);

        Assert.Equal((2, 0), (counts[mentor], counts[other]));
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, "/internal/mentors/team-counts"), (request.Method, request.RequestUri!.AbsolutePath));
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(semester, json.GetProperty("semesterId").GetGuid());
        Assert.Equal(2, json.GetProperty("mentorIds").GetArrayLength());
    }

    [Fact]
    public async Task Empty_list_does_not_call_team()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("không được gọi"));
        Assert.Empty(await Client(handler).GetMentorTeamCountsAsync([], Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Error_status_becomes_team_unavailable(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)));
        await Assert.ThrowsAsync<TeamUnavailableException>(() =>
            Client(handler).GetMentorTeamCountsAsync([Guid.NewGuid()], Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Connection_failure_and_timeout_become_team_unavailable()
    {
        var refused = new StubHandler(_ => throw new HttpRequestException("Connection refused"));
        await Assert.ThrowsAsync<TeamUnavailableException>(() =>
            Client(refused).GetMentorTeamCountsAsync([Guid.NewGuid()], Guid.NewGuid(), CancellationToken.None));

        var timeout = new StubHandler(_ => throw new TaskCanceledException("timeout"));
        await Assert.ThrowsAsync<TeamUnavailableException>(() =>
            Client(timeout).GetMentorTeamCountsAsync([Guid.NewGuid()], Guid.NewGuid(), CancellationToken.None));
    }
}
