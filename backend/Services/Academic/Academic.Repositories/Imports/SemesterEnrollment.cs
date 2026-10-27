// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Imports;

/// <summary>Bảng <c>academic.semester_enrollments</c> – danh sách sinh viên của học kỳ (nguồn whitelist). Mỗi sinh viên một dòng mỗi học kỳ.</summary>
public class SemesterEnrollment
{
    public Guid Id { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StudentId { get; set; }

    /// <summary><c>EXE101</c> hoặc <c>EXE201</c>.</summary>
    public string CourseCode { get; set; } = string.Empty;

    public int ClassCodeId { get; set; }
    public Guid? ImportBatchId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
