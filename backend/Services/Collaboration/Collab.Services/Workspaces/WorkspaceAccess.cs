using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Collab.Repositories.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Collab.Services.Workspaces;

/// <summary>Vai trò của người gọi trong một workspace. Admin xem được mọi workspace nhưng chỉ đọc (rule 21).</summary>
public sealed record WorkspaceAccess(Guid WorkspaceId, string Role, string PersonType, Guid PersonId)
{
    public const string AdminRole = "ADMIN";

    public bool IsStudent => Role is WorkspaceRoles.Leader or WorkspaceRoles.Member;
    public bool IsLeader => Role == WorkspaceRoles.Leader;
    public bool IsInstructor => Role == WorkspaceRoles.Instructor;
    public bool IsMentor => Role == WorkspaceRoles.Mentor;
    public bool IsAdvisor => IsInstructor || IsMentor;
    public bool IsReadOnly => Role == AdminRole;
}

public interface IWorkspaceAccessService
{
    /// <summary>Ném 403 <c>not-workspace-member</c> nếu người gọi không thuộc workspace.</summary>
    Task<WorkspaceAccess> RequireAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Danh sách (loại, id) của người gọi theo claim: sinh viên, giảng viên, mentor.</summary>
    IReadOnlyCollection<(string Type, Guid Id)> PeopleOf(ClaimsPrincipal user);
}

public sealed class WorkspaceAccessService(IWorkspaceRepository workspaces) : IWorkspaceAccessService
{
    public async Task<WorkspaceAccess> RequireAsync(Guid workspaceId, ClaimsPrincipal user, CancellationToken ct)
    {
        var people = PeopleOf(user);
        if (people.Count > 0 && await workspaces.FindActiveMemberAsync(workspaceId, people, ct) is { } member)
        {
            return new WorkspaceAccess(workspaceId, member.Role, member.PersonType, member.PersonId);
        }

        if (user.IsInRole(Roles.Admin))
        {
            if (await workspaces.GetByIdAsync(workspaceId, ct) is null)
            {
                throw new NotFoundException("workspace", workspaceId);
            }
            return new WorkspaceAccess(workspaceId, WorkspaceAccess.AdminRole, "ADMIN", user.GetUserId());
        }

        throw new ForbiddenException("not-workspace-member", "Bạn không thuộc dự án này");
    }

    public IReadOnlyCollection<(string Type, Guid Id)> PeopleOf(ClaimsPrincipal user)
    {
        var people = new List<(string, Guid)>(3);
        if (user.GetStudentId() is { } studentId)
        {
            people.Add((PersonTypes.Student, studentId));
        }
        if (user.GetLecturerId() is { } lecturerId)
        {
            people.Add((PersonTypes.Lecturer, lecturerId));
        }
        if (user.GetMentorId() is { } mentorId)
        {
            people.Add((PersonTypes.Mentor, mentorId));
        }
        return people;
    }
}

/// <summary>
/// Policy <c>WorkspaceMember</c>: route có <c>{workspaceId}</c> chỉ cho thành viên, cố vấn đang hiệu lực và Admin.
/// Dùng: <c>[Authorize(Policy = CollabPolicies.WorkspaceMember)]</c>.
/// </summary>
public static class CollabPolicies
{
    public const string WorkspaceMember = "WorkspaceMember";
}

public sealed class WorkspaceMemberRequirement : IAuthorizationRequirement;

public sealed class WorkspaceMemberHandler(IHttpContextAccessor httpContextAccessor, IWorkspaceAccessService access)
    : AuthorizationHandler<WorkspaceMemberRequirement>
{
    public const string AccessItemKey = "collab.workspace-access";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WorkspaceMemberRequirement requirement)
    {
        var http = httpContextAccessor.HttpContext;
        if (http?.GetRouteValue("workspaceId") is not string raw || !Guid.TryParse(raw, out var workspaceId))
        {
            return;
        }

        // Lỗi 403 có mã not-workspace-member do GlobalExceptionHandler trả về.
        var result = await access.RequireAsync(workspaceId, context.User, http.RequestAborted);
        http.Items[AccessItemKey] = result;
        context.Succeed(requirement);
    }
}

public static class WorkspaceAccessHttpExtensions
{
    /// <summary>Quyền đã kiểm tra bởi policy <c>WorkspaceMember</c> trong request hiện tại.</summary>
    public static WorkspaceAccess GetWorkspaceAccess(this HttpContext context) =>
        context.Items[WorkspaceMemberHandler.AccessItemKey] as WorkspaceAccess
        ?? throw new InvalidOperationException("Action chưa gắn policy WorkspaceMember.");
}
