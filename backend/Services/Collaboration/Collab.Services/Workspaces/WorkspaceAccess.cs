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
/// Policy theo workspace cho route có <c>{workspaceId}</c> (BE-14):
/// <list type="bullet">
/// <item><c>WorkspaceMember</c> – thành viên, cố vấn đang hiệu lực và Admin (Admin chỉ đọc).</item>
/// <item><c>WorkspaceWriter</c> – như trên nhưng không có Admin: dùng cho thao tác ghi (gửi tin, tạo việc, tải file…).</item>
/// <item><c>WorkspaceAdvisor</c> – chỉ giảng viên hướng dẫn hoặc mentor của workspace (đánh dấu checkpoint, dời lịch họp…).</item>
/// </list>
/// Dùng: <c>[Authorize(Policy = CollabPolicies.WorkspaceAdvisor)]</c>, rồi đọc vai trò bằng <c>HttpContext.GetWorkspaceAccess()</c>.
/// Kiểm tra trưởng nhóm (Leader) làm ở tầng Services bằng <c>access.IsLeader</c>, không phải policy.
/// </summary>
public static class CollabPolicies
{
    public const string WorkspaceMember = "WorkspaceMember";
    public const string WorkspaceWriter = "WorkspaceWriter";
    public const string WorkspaceAdvisor = "WorkspaceAdvisor";
}

public enum WorkspaceAccessLevel
{
    Member,
    Writer,
    Advisor,
}

public sealed record WorkspaceMemberRequirement(WorkspaceAccessLevel Level = WorkspaceAccessLevel.Member) : IAuthorizationRequirement;

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

        // Các lỗi 403 có mã (not-workspace-member, workspace-read-only, not-workspace-advisor) do GlobalExceptionHandler trả về.
        var result = http.Items[AccessItemKey] as WorkspaceAccess is { } cached && cached.WorkspaceId == workspaceId
            ? cached
            : await access.RequireAsync(workspaceId, context.User, http.RequestAborted);
        http.Items[AccessItemKey] = result;
        Check(result, requirement.Level);
        context.Succeed(requirement);
    }

    public static void Check(WorkspaceAccess access, WorkspaceAccessLevel level)
    {
        switch (level)
        {
            case WorkspaceAccessLevel.Writer when access.IsReadOnly:
                throw new ForbiddenException("workspace-read-only", "Bộ môn chỉ xem được dự án này");
            case WorkspaceAccessLevel.Advisor when !access.IsAdvisor:
                throw new ForbiddenException("not-workspace-advisor", "Chỉ giảng viên hướng dẫn hoặc mentor của nhóm làm được việc này");
        }
    }
}

public static class WorkspacePolicyExtensions
{
    /// <summary>Đăng ký ba policy workspace và handler của chúng.</summary>
    public static AuthorizationBuilder AddWorkspacePolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(CollabPolicies.WorkspaceMember, p => p.RequireAuthenticatedUser().AddRequirements(new WorkspaceMemberRequirement()))
        .AddPolicy(CollabPolicies.WorkspaceWriter, p => p.RequireAuthenticatedUser().AddRequirements(new WorkspaceMemberRequirement(WorkspaceAccessLevel.Writer)))
        .AddPolicy(CollabPolicies.WorkspaceAdvisor, p => p.RequireAuthenticatedUser().AddRequirements(new WorkspaceMemberRequirement(WorkspaceAccessLevel.Advisor)));
}

public static class WorkspaceAccessHttpExtensions
{
    /// <summary>Quyền đã kiểm tra bởi policy <c>WorkspaceMember</c> trong request hiện tại.</summary>
    public static WorkspaceAccess GetWorkspaceAccess(this HttpContext context) =>
        context.Items[WorkspaceMemberHandler.AccessItemKey] as WorkspaceAccess
        ?? throw new InvalidOperationException("Action chưa gắn policy WorkspaceMember.");
}
