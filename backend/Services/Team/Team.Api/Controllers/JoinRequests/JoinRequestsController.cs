using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Invitations;
using Team.Services.JoinRequests;
using Team.Services.Members;

namespace Team.Api.Controllers.JoinRequests;

/// <summary>Yêu cầu tham gia: S09, S12, S20 (BE-29, rule 8, 17) – team.yaml tag JoinRequests.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class JoinRequestsController(IJoinRequestService requests) : ControllerBase
{
    [HttpGet("teams/{teamId:guid}/join-requests")]
    public async Task<ActionResult<IReadOnlyList<JoinRequestResponse>>> ListForTeam(Guid teamId, [FromQuery] RequestStatusQuery query, CancellationToken ct) =>
        Ok(await requests.ListForTeamAsync(teamId, query.Status, User, ct));

    /// <summary>Xin vào nhóm kèm 1–3 ý tưởng hoặc hasNoIdea = true; hết hạn sau 72 giờ.</summary>
    [HttpPost("teams/{teamId:guid}/join-requests")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<JoinRequestResponse>> Submit(Guid teamId, SubmitJoinRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await requests.SubmitAsync(teamId, request, User, ct));

    [HttpGet("join-requests/me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<IReadOnlyList<JoinRequestResponse>>> Mine([FromQuery] RequestStatusQuery query, CancellationToken ct) =>
        Ok(await requests.ListMineAsync(query.Status, User, ct));

    [HttpPost("join-requests/{id:guid}/accept")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<MemberChangeResult>> Accept(Guid id, CancellationToken ct) => Ok(await requests.AcceptAsync(id, User, ct));

    [HttpPost("join-requests/{id:guid}/decline")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        await requests.DeclineAsync(id, User, ct);
        return NoContent();
    }

    [HttpPost("join-requests/{id:guid}/cancel")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await requests.CancelAsync(id, User, ct);
        return NoContent();
    }
}
