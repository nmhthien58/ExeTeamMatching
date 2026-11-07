using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.LeaderVotes;

namespace Team.Api.Controllers.LeaderVotes;

/// <summary>Bỏ phiếu đổi nhóm trưởng: S15, S16 (BE-88) – team.yaml tag LeaderVotes.</summary>
[ApiController]
[Authorize(Policy = Policies.Student)]
[Route("api/teams/{teamId:guid}/leader-votes")]
public sealed class LeaderVotesController(ILeaderVoteService votes) : ControllerBase
{
    /// <summary>Đề nghị đang mở; 204 nếu không có.</summary>
    [HttpGet("current")]
    public async Task<ActionResult<LeaderVoteResponse>> Current(Guid teamId, CancellationToken ct) =>
        await votes.GetCurrentAsync(teamId, User, ct) is { } vote ? Ok(vote) : NoContent();

    [HttpPost]
    public async Task<ActionResult<LeaderVoteResponse>> Open(Guid teamId, OpenLeaderVoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await votes.OpenAsync(teamId, request, User, ct));

    [HttpPut("{voteId:guid}/ballot")]
    public async Task<ActionResult<LeaderVoteResponse>> Ballot(Guid teamId, Guid voteId, CastBallotRequest request, CancellationToken ct) =>
        Ok(await votes.CastBallotAsync(teamId, voteId, request, User, ct));

    [HttpDelete("{voteId:guid}")]
    public async Task<IActionResult> Withdraw(Guid teamId, Guid voteId, CancellationToken ct)
    {
        await votes.WithdrawAsync(teamId, voteId, User, ct);
        return NoContent();
    }
}
