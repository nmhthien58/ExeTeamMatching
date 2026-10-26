// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.LecturerPreferences;

/// <summary>Bảng <c>team.lecturer_preferences</c> – nguyện vọng giảng viên có thứ tự (rule 9).</summary>
public class LecturerPreference
{
    public Guid TeamId { get; set; }
    public short Rank { get; set; }
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public Guid UpdatedByStudentId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
