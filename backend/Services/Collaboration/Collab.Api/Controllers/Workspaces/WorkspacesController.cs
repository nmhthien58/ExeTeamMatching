using Collab.Repositories.Workspaces;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Workspaces;

/// <summary>Workspace của người dùng (khung BE-62). Số liệu từng module (tin chưa đọc, việc, họp…) do module đó bổ sung.</summary>
[ApiController]
[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceRepository workspaces, IWorkspaceAccessService access) : ControllerBase
{
    /// <summary>Workspace của tôi – sinh viên có một, giảng viên / mentor có nhiều (C02, M01, L04).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummaryResponse>>> Mine(CancellationToken ct)
    {
        var rows = await workspaces.ListForPeopleAsync(access.PeopleOf(User), ct);
        return Ok(rows.Select(r => WorkspaceSummaryResponse.From(r.Workspace, r.Role)).ToList());
    }

    /// <summary>Thông tin chung của một workspace và vai trò của tôi (đầu trang C01).</summary>
    [HttpGet("{workspaceId:guid}")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<WorkspaceSummaryResponse>> Get(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct)
            ?? throw new BuildingBlocks.Errors.NotFoundException("workspace", workspaceId);
        return Ok(WorkspaceSummaryResponse.From(workspace, HttpContext.GetWorkspaceAccess().Role));
    }
}

public sealed record WorkspaceSummaryResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string CourseCode,
    string MyRole,
    Guid? ProjectId,
    Guid? PreviousWorkspaceId)
{
    public static WorkspaceSummaryResponse From(Workspace w, string role) =>
        new(w.Id, w.TeamId, w.TeamName, w.CourseCode, role, w.ProjectId, w.PreviousWorkspaceId);
}
