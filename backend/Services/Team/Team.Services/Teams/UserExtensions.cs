using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Repositories.AuditLog;

namespace Team.Services.Teams;

public static class UserExtensions
{
    /// <summary>Người thực hiện cho nhật ký (rule 10): Admin hoặc sinh viên.</summary>
    public static AuditActor ToAuditActor(this ClaimsPrincipal user) =>
        new(user.GetUserId(), user.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Student, user.Identity?.Name);

    /// <summary>Chỉ có vai trò sinh viên (không kiêm Admin / giảng viên / mentor).</summary>
    public static bool IsStudentOnly(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Student) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Lecturer) && !user.IsInRole(Roles.Mentor);
}
