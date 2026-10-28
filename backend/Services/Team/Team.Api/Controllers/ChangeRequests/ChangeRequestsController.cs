using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.ChangeRequests;
using Team.Services.Teams;

namespace Team.Api.Controllers.ChangeRequests;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ChangeRequestsController(IChangeRequestService requests) : ControllerBase
{
    [HttpGet("teams/{teamId:guid}/pending-changes")]
    public async Task<ActionResult<PendingChangesResponse>> Pending(Guid teamId, CancellationToken ct) =>
        Ok(await requests.GetPendingChangesAsync(teamId, User, ct));

    [HttpPut("teams/{teamId:guid}/pending-changes/project")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<ChangeItemResponse>> SetProjectChange(Guid teamId, ProjectChangeInput input, CancellationToken ct) =>
        Ok(await requests.SetProjectChangeAsync(teamId, input, User, ct));

    [HttpDelete("teams/{teamId:guid}/pending-changes/project")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> DeleteProjectChange(Guid teamId, CancellationToken ct)
    {
        await requests.DeleteProjectChangeAsync(teamId, User, ct);
        return NoContent();
    }

    [HttpGet("teams/{teamId:guid}/change-requests")]
    public async Task<ActionResult<IReadOnlyList<ChangeRequestResponse>>> ForTeam(Guid teamId, CancellationToken ct) =>
        Ok(await requests.ListForTeamAsync(teamId, User, ct));

    [HttpPost("teams/{teamId:guid}/change-requests")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<ChangeRequestResponse>> Submit(Guid teamId, ChangeRequestInput input, CancellationToken ct)
    {
        var result = await requests.SubmitAsync(teamId, input, User, ct);
        return Created($"/api/change-requests/{result.Id}", result);
    }

    [HttpGet("change-requests")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<PagedResponse<ChangeRequestResponse>>> Search([FromQuery] ChangeRequestQuery query, CancellationToken ct) =>
        Ok(await requests.SearchAsync(query, ct));

    [HttpGet("change-requests/{id:guid}")]
    public async Task<ActionResult<ChangeRequestDetailResponse>> Detail(Guid id, CancellationToken ct) =>
        Ok(await requests.GetDetailAsync(id, User, ct));

    [HttpPost("change-requests/{id:guid}/approve")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<ChangeRequestResponse>> Approve(Guid id, ChangeRequestDecisionInput input, CancellationToken ct) =>
        Ok(await requests.ApproveAsync(id, input, User, ct));

    [HttpPost("change-requests/{id:guid}/reject")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<ChangeRequestResponse>> Reject(Guid id, ChangeRequestDecisionInput input, CancellationToken ct) =>
        Ok(await requests.RejectAsync(id, input, User, ct));
}
