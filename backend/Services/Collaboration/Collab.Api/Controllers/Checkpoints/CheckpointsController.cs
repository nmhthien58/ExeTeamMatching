using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Collab.Services.Checkpoints;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Checkpoints;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CheckpointsController(ICheckpointService checkpoints) : ControllerBase
{
    [HttpGet("workspaces/{workspaceId:guid}/checkpoints")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<CheckpointProgress>> Get(Guid workspaceId, CancellationToken ct) =>
        Ok(await checkpoints.GetAsync(workspaceId, ct));

    [HttpPut("workspaces/{workspaceId:guid}/checkpoints/{checkpointNo:int}")]
    [Authorize(Policy = CollabPolicies.WorkspaceAdvisor)]
    public async Task<ActionResult<CheckpointProgress>> Mark(Guid workspaceId, int checkpointNo,
        MarkCheckpointRequest request, CancellationToken ct) =>
        Ok(await checkpoints.MarkAsync(workspaceId, checkpointNo, request, HttpContext.GetWorkspaceAccess(), ct));

    [HttpDelete("workspaces/{workspaceId:guid}/checkpoints/{checkpointNo:int}")]
    [Authorize(Policy = CollabPolicies.WorkspaceAdvisor)]
    public async Task<IActionResult> Undo(Guid workspaceId, int checkpointNo, CancellationToken ct)
    {
        await checkpoints.UndoAsync(workspaceId, checkpointNo, HttpContext.GetWorkspaceAccess(), ct);
        return NoContent();
    }

    [HttpGet("checkpoints/me")]
    public async Task<ActionResult<IReadOnlyList<CheckpointProgress>>> Mine([FromQuery] int? missing, CancellationToken ct)
    {
        var lecturerId = User.GetLecturerId();
        if (lecturerId is null)
            throw new ForbiddenException("lecturer-only", "Chỉ giảng viên được xem danh sách này");
        return Ok(await checkpoints.MineAsync(lecturerId.Value, missing, ct));
    }
}
