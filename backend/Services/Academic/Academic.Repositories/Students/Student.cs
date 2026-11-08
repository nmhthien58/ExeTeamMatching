// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Students;

/// <summary>Bảng <c>academic.students</c> – hồ sơ sinh viên, ổn định qua các học kỳ. MSSV 2 chữ + 6 số.</summary>
public class Student
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>Cột <c>citext</c>: so sánh không phân biệt hoa thường.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Chỉ đến từ file import, sinh viên không sửa (rule 11).</summary>
    public string? Phone { get; set; }

    public int MajorId { get; set; }

    public int? SpecializationId { get; set; }

    /// <summary>Giới thiệu ngắn (rule 16).</summary>
    public string? Description { get; set; }

    /// <summary>Hoàn tất A05 (đã đánh giá đủ 5 kỹ năng).</summary>
    public DateTimeOffset? ProfileCompletedAt { get; set; }

    /// <summary>Công tắc "Tôi đang tìm nhóm" (S03).</summary>
    public bool IsSeekingTeam { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
