// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Students;

/// <summary>Bảng <c>academic.student_skills</c> – 5 kỹ năng cố định, tự đánh giá 1–5 (rule 16). Thiếu dòng = chưa đánh giá.</summary>
public class StudentSkill
{
    public Guid StudentId { get; set; }

    /// <summary><c>TECHNOLOGY</c>, <c>UI_UX_DESIGN</c>, <c>MANAGEMENT</c>, <c>MARKETING</c>, <c>COMMUNICATION_PLANNING</c>.</summary>
    public string Skill { get; set; } = string.Empty;

    public short Level { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
