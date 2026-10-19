// Map deploy/db/01_identity_db.sql (BE-10). Sửa DB script trước, rồi sửa file này cho khớp.
using BuildingBlocks.Auth;

namespace Identity.Repositories.Accounts;

/// <summary>Bảng <c>identity.roles</c> – dữ liệu cố định ở deploy/db/90_seed_catalogs.sql.</summary>
public class Role
{
    public short Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Id cố định của bảng <c>roles</c>.</summary>
public static class RoleIds
{
    public const short Student = 1;
    public const short Lecturer = 2;
    public const short Admin = 3;
    public const short Mentor = 4;

    public static string ToCode(short id) => id switch
    {
        Student => Roles.Student,
        Lecturer => Roles.Lecturer,
        Admin => Roles.Admin,
        Mentor => Roles.Mentor,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Role không tồn tại."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "student" => Student,
        "lecturer" => Lecturer,
        "admin" => Admin,
        "mentor" => Mentor,
        _ => null,
    };

    /// <summary>Student, Lecturer, Mentor cần <c>external_ref_id</c>; Admin thì không (ck_user_account_roles_external_ref).</summary>
    public static bool NeedsExternalRef(short id) => id != Admin;
}
