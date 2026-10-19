// Map deploy/db/01_identity_db.sql (BE-10). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Identity.Repositories.Accounts;

/// <summary>
/// Bảng <c>identity.user_account_roles</c>. <see cref="ExternalRefId"/> = id ở Academic
/// (students / lecturers / mentors), đưa vào JWT thành student_id / lecturer_id / mentor_id.
/// </summary>
public class UserAccountRole
{
    public Guid UserAccountId { get; set; }
    public short RoleId { get; set; }
    public Guid? ExternalRefId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
