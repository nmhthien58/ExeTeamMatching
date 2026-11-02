using Collab.Repositories.Files;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Workspaces;

/// <summary>Workspace của người dùng (khung BE-62). Số liệu từng module (tin chưa đọc, việc, họp…) do module đó bổ sung.</summary>
[ApiController]
[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceRepository workspaces, IWorkspaceLookup lookup, IWorkspaceAccessService access) : ControllerBase
{
    /// <summary>Workspace của tôi – sinh viên có một, giảng viên / mentor có nhiều (C02, M01, L04).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummaryResponse>>> Mine(CancellationToken ct)
    {
        var rows = await workspaces.ListForPeopleAsync(access.PeopleOf(User), ct);
        return Ok(rows.Select(r => WorkspaceSummaryResponse.From(r.Workspace, r.Role)).ToList());
    }

    /// <summary>Thông tin chung của một workspace, vai trò của tôi và người đang ở workspace (đầu trang C01, gợi ý @ ở C03).</summary>
    [HttpGet("{workspaceId:guid}")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<WorkspaceOverviewResponse>> Get(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct)
            ?? throw new BuildingBlocks.Errors.NotFoundException("workspace", workspaceId);
        var members = await lookup.ListMembersAsync(workspaceId, ct);
        return Ok(WorkspaceOverviewResponse.From(workspace, HttpContext.GetWorkspaceAccess().Role, members));
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

/// <summary>Schema <c>WorkspaceOverview</c>: <see cref="People"/> chỉ gồm người chưa rời, sinh viên trước rồi tới cố vấn.</summary>
public sealed record WorkspaceOverviewResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string CourseCode,
    string MyRole,
    Guid? ProjectId,
    Guid? PreviousWorkspaceId,
    IReadOnlyList<PersonRef> People)
{
    private static readonly string[] RoleOrder = [WorkspaceRoles.Leader, WorkspaceRoles.Member, WorkspaceRoles.Instructor, WorkspaceRoles.Mentor];

    public static WorkspaceOverviewResponse From(Workspace w, string role, IEnumerable<WorkspaceMember> members) =>
        new(w.Id, w.TeamId, w.TeamName, w.CourseCode, role, w.ProjectId, w.PreviousWorkspaceId,
            members.Where(m => m.LeftAt is null)
                .OrderBy(m => Array.IndexOf(RoleOrder, m.Role)).ThenBy(m => m.DisplayName)
                .Select(m => new PersonRef(m.PersonType, m.PersonId, m.DisplayName, m.Role))
                .ToList());
}
