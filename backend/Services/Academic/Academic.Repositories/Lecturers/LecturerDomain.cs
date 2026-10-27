// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Lecturers;

/// <summary>Bảng <c>academic.lecturer_domains</c> – chuyên môn của giảng viên (rule 12, 19).</summary>
public class LecturerDomain
{
    public Guid LecturerId { get; set; }
    public int DomainId { get; set; }
}
