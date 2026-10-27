// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Mentors;

/// <summary>
/// Bảng <c>academic.mentors</c> – mentor do Bộ môn tạo (rule 20). Người ngoài trường, hoặc giảng viên của trường
/// (<see cref="LecturerId"/>: cùng người với một giảng viên, khi đó <see cref="IsInternal"/> phải true).
/// </summary>
public class Mentor
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Cột <c>citext</c>: so sánh không phân biệt hoa thường.</summary>
    public string Email { get; set; } = string.Empty;

    public string? Organization { get; set; }
    public bool IsInternal { get; set; }
    public Guid? LecturerId { get; set; }
    public string? Description { get; set; }

    /// <summary>Do Bộ môn đặt, 1–50.</summary>
    public short MaxTeams { get; set; }

    /// <summary>Mentor tự chọn, không vượt <see cref="MaxTeams"/>.</summary>
    public short? PreferredTeams { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary><c>identity.user_accounts.id</c> (Admin).</summary>
    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
