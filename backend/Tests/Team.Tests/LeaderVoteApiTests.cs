using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.LeaderVotes;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.LeaderVotes;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>BE-88: bỏ phiếu đổi nhóm trưởng (docs/de-xuat-bau-nhom-truong.md mục 8).</summary>
public sealed class LeaderVoteApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    public void Required_approvals_is_more_than_half(int members, int expected) => Assert.Equal(expected, LeaderVoteRules.RequiredApprovals(members));

    [Theory]
    [InlineData(5, 3, 3, LeaderVoteStatuses.Passed)]
    [InlineData(5, 2, 3, null)]
    [InlineData(5, 1, 3, null)]
    [InlineData(5, 1, 4, LeaderVoteStatuses.Rejected)]
    [InlineData(5, 1, 5, LeaderVoteStatuses.Rejected)]
    [InlineData(4, 1, 3, LeaderVoteStatuses.Rejected)]
    public void Outcome_closes_as_soon_as_it_is_decided(int members, int approvals, int ballots, string? expected) =>
        Assert.Equal(expected, LeaderVoteRules.Outcome(members, approvals, ballots));

    [Fact]
    public async Task Enough_approvals_change_the_leader_and_publish_events()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, leader, others) = await TeamOfAsync(4);
        var vote = await OpenAsync(team.Id, others[0], others[1]);
        Assert.Equal((1, 3, true), (vote.ApproveCount, vote.RequiredApprovals, vote.CanWithdraw));

        Assert.Equal(LeaderVoteStatuses.Open, (await BallotAsync(team.Id, vote.Id, others[1], true)).Status);
        var closed = await BallotAsync(team.Id, vote.Id, others[2], true);

        Assert.Equal(LeaderVoteStatuses.Passed, closed.Status);
        var detail = await _api.ClientFor(leader).GetFromJsonAsync<TeamDetailResponse>($"/api/teams/{team.Id}");
        Assert.Equal(others[1], detail!.Leader!.Id);
        var events = (await _api.EventsAsync(team.Id)).Select(e => e.EventType).ToList();
        Assert.Contains(MemberEventTypes.LeaderChanged, events);
        Assert.Contains(LeaderVoteEventTypes.LeaderVoteOpened, events);
        Assert.Contains(LeaderVoteEventTypes.LeaderVoteClosed, events);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.ClientFor(leader).GetAsync($"/api/teams/{team.Id}/leader-votes/current")).StatusCode);
    }

    [Fact]
    public async Task Vote_is_rejected_early_and_ballot_can_be_changed()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, leader, others) = await TeamOfAsync(4);
        var vote = await OpenAsync(team.Id, others[0], others[1]);

        Assert.False((await BallotAsync(team.Id, vote.Id, others[1], false)).MyBallot);
        Assert.True((await BallotAsync(team.Id, vote.Id, others[1], true)).MyBallot);
        await BallotAsync(team.Id, vote.Id, others[1], false);

        Assert.Equal(LeaderVoteStatuses.Rejected, (await BallotAsync(team.Id, vote.Id, leader, false)).Status);
    }

    [Fact]
    public async Task Only_one_open_vote_and_the_leader_cannot_propose()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, leader, others) = await TeamOfAsync(4);
        await OpenAsync(team.Id, others[0], others[1]);

        var second = await _api.ClientFor(others[1]).PostAsJsonAsync($"/api/teams/{team.Id}/leader-votes", new OpenLeaderVoteRequest(others[2], null));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("leader-vote-open", await second.Content.ReadAsStringAsync());

        var (other, otherLeader, otherMembers) = await TeamOfAsync(4);
        var byLeader = await _api.ClientFor(otherLeader).PostAsJsonAsync($"/api/teams/{other.Id}/leader-votes", new OpenLeaderVoteRequest(otherMembers[0], null));
        Assert.Equal(HttpStatusCode.BadRequest, byLeader.StatusCode);
    }

    [Fact]
    public async Task Finalized_team_cannot_open_a_vote()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, _, others) = await TeamOfAsync(4);
        await _api.QueryDbAsync(async db =>
        {
            var entity = await db.Set<TeamEntity>().SingleAsync(t => t.Id == team.Id);
            (entity.Status, entity.FinalizedAt) = (TeamStatuses.Finalized, DateTimeOffset.UtcNow);
            return await db.SaveChangesAsync();
        });

        var response = await _api.ClientFor(others[0]).PostAsJsonAsync($"/api/teams/{team.Id}/leader-votes", new OpenLeaderVoteRequest(others[1], null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("leader-vote-not-allowed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Expired_vote_is_closed_when_read()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, leader, others) = await TeamOfAsync(4);
        var vote = await OpenAsync(team.Id, others[0], others[1]);
        await _api.QueryDbAsync(async db =>
        {
            var entity = await db.Set<LeaderVote>().SingleAsync(v => v.Id == vote.Id);
            entity.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, (await _api.ClientFor(leader).GetAsync($"/api/teams/{team.Id}/leader-votes/current")).StatusCode);
        Assert.Equal(LeaderVoteStatuses.Expired, await _api.QueryDbAsync(db => db.Set<LeaderVote>().Where(v => v.Id == vote.Id).Select(v => v.Status).SingleAsync()));
    }

    [Fact]
    public async Task Nominee_leaving_or_leader_transfer_cancels_the_vote()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, leader, others) = await TeamOfAsync(5);
        var vote = await OpenAsync(team.Id, others[0], others[1]);
        Assert.True((await _api.ClientFor(others[1]).PostAsJsonAsync($"/api/teams/{team.Id}/leave", new { })).IsSuccessStatusCode);
        Assert.Equal(LeaderVoteStatuses.Cancelled, await StatusAsync(vote.Id));

        var next = await OpenAsync(team.Id, others[0], others[2]);
        Assert.True((await _api.ClientFor(leader).PostAsJsonAsync($"/api/teams/{team.Id}/transfer-leader", new TransferLeaderRequest(others[3]))).IsSuccessStatusCode);
        Assert.Equal(LeaderVoteStatuses.Cancelled, await StatusAsync(next.Id));
    }

    [Fact]
    public async Task Only_the_proposer_can_withdraw()
    {
        if (!Enabled)
        {
            return;
        }
        var (team, _, others) = await TeamOfAsync(4);
        var vote = await OpenAsync(team.Id, others[0], others[1]);

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ClientFor(others[1]).DeleteAsync($"/api/teams/{team.Id}/leader-votes/{vote.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.ClientFor(others[0]).DeleteAsync($"/api/teams/{team.Id}/leader-votes/{vote.Id}")).StatusCode);
        Assert.Equal(LeaderVoteStatuses.Cancelled, await StatusAsync(vote.Id));
    }

    private Task<string> StatusAsync(Guid voteId) => _api.QueryDbAsync(db => db.Set<LeaderVote>().Where(v => v.Id == voteId).Select(v => v.Status).SingleAsync());

    private async Task<LeaderVoteResponse> OpenAsync(Guid teamId, Guid proposer, Guid nominee)
    {
        var response = await _api.ClientFor(proposer).PostAsJsonAsync($"/api/teams/{teamId}/leader-votes", new OpenLeaderVoteRequest(nominee, "Bạn ấy sát sao hơn"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LeaderVoteResponse>())!;
    }

    private async Task<LeaderVoteResponse> BallotAsync(Guid teamId, Guid voteId, Guid student, bool approve)
    {
        var response = await _api.ClientFor(student).PutAsJsonAsync($"/api/teams/{teamId}/leader-votes/{voteId}/ballot", new CastBallotRequest(approve));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LeaderVoteResponse>())!;
    }

    private async Task<(TeamDetailResponse Team, Guid Leader, List<Guid> Others)> TeamOfAsync(int size)
    {
        var team = await TestData.CreateTeamAsync(_api, "Nhóm " + Guid.NewGuid().ToString()[..6], extraMembers: size - 1);
        var ids = await _api.QueryDbAsync(db => db.Set<Team.Repositories.Members.TeamMember>().Where(m => m.TeamId == team.Id).Select(m => m.StudentId).ToListAsync());
        var leader = team.Leader!.Id;
        return (team, leader, ids.Where(id => id != leader).ToList());
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
