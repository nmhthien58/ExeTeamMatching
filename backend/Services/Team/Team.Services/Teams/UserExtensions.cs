using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Repositories.AuditLog;

namespace Team.Services.Teams;

public static class UserExtensions
{
    /// <summary>
    /// Người thực hiện cho nhật ký (rule 10). Vai trò ghi theo thứ tự Admin → Lecturer → Mentor → Student;
    /// tài khoản giữ nhiều vai trò (giảng viên kiêm mentor) truyền <paramref name="actingAs"/> để ghi đúng vai trò của thao tác.
    /// </summary>
    public static AuditActor ToAuditActor(this ClaimsPrincipal user, string? actingAs = null) =>
        new(user.GetUserId(), AuditRole(user, actingAs), user.Identity?.Name);

    /// <summary>Chỉ có vai trò sinh viên (không kiêm Admin / giảng viên / mentor).</summary>
    public static bool IsStudentOnly(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Student) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Lecturer) && !user.IsInRole(Roles.Mentor);

    private static readonly string[] AuditRoles = [Roles.Admin, Roles.Lecturer, Roles.Mentor, Roles.Student];

    private static string AuditRole(ClaimsPrincipal user, string? actingAs)
    {
        if (actingAs is not null && AuditRoles.Contains(actingAs) && user.IsInRole(actingAs))
        {
            return actingAs;
        }
        return AuditRoles.FirstOrDefault(user.IsInRole) ?? Roles.Student;
    }
}
