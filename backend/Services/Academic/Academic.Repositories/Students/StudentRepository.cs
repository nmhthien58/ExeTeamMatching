using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Students;

/// <summary>
/// Snapshot sinh viên trong một học kỳ. <see cref="CourseCode"/> lấy từ <c>semester_enrollments</c> của học kỳ đó;
/// null khi sinh viên không có trong danh sách học kỳ.
/// </summary>
public sealed record StudentSnapshotRow(Guid Id, string StudentCode, string FullName, int SpecializationId, string? CourseCode);

public interface IStudentRepository
{
    /// <summary>Sinh viên theo id; id không tồn tại thì không có trong kết quả.</summary>
    Task<IReadOnlyList<StudentSnapshotRow>> LookupAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Sinh viên theo MSSV (đã viết hoa); MSSV không tồn tại thì không có trong kết quả.</summary>
    Task<IReadOnlyList<StudentSnapshotRow>> LookupByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct);
}

public sealed class StudentRepository(AcademicDbContext db) : IStudentRepository
{
    public Task<IReadOnlyList<StudentSnapshotRow>> LookupAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        SnapshotsAsync(db.Students.Where(s => studentIds.Contains(s.Id)), semesterId, ct);

    public Task<IReadOnlyList<StudentSnapshotRow>> LookupByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct) =>
        SnapshotsAsync(db.Students.Where(s => studentCodes.Contains(s.StudentCode)), semesterId, ct);

    /// <summary>Một câu SQL: sinh viên kèm môn của họ trong học kỳ (subquery trên <c>uq_semester_enrollments_semester_student</c>).</summary>
    private async Task<IReadOnlyList<StudentSnapshotRow>> SnapshotsAsync(IQueryable<Student> students, Guid? semesterId, CancellationToken ct) =>
        await students.AsNoTracking()
            .OrderBy(s => s.StudentCode)
            .Select(s => new StudentSnapshotRow(s.Id, s.StudentCode, s.FullName, s.SpecializationId,
                db.SemesterEnrollments.Where(e => e.StudentId == s.Id && e.SemesterId == semesterId).Select(e => e.CourseCode).FirstOrDefault()))
            .ToListAsync(ct);
}
