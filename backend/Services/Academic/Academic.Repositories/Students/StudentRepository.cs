using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Students;

/// <summary>
/// Snapshot sinh viên trong một học kỳ. <see cref="CourseCode"/>, <see cref="ClassCode"/> lấy từ <c>semester_enrollments</c> của học kỳ đó;
/// null khi sinh viên không có trong danh sách học kỳ.
/// </summary>
public sealed record StudentSnapshotRow(Guid Id, string StudentCode, string FullName, int? SpecializationId, string? CourseCode, string? ClassCode, int MajorId = 0);

/// <summary>Bộ lọc danh sách sinh viên của một học kỳ. <see cref="Sort"/>: name, -name, studentCode, -studentCode.</summary>
public sealed record StudentDirectoryFilter(
    Guid SemesterId, string? CourseCode, string? Search, string? ClassCode, int? SpecializationId, string? Sort, int Page, int PageSize);

/// <summary>Dòng danh sách sinh viên: class có <c>init</c> để EF dịch được Where/OrderBy sau khi chiếu.</summary>
/// <summary>Sinh viên đang bật "Tôi đang tìm nhóm" kèm giới thiệu và kỹ năng (mục "SV tìm nhóm").</summary>
public sealed record SeekingStudentRow(
    Guid Id, string StudentCode, string FullName, int? SpecializationId, int MajorId, string? Description,
    IReadOnlyList<(string Skill, short Level)> Skills);

public sealed class StudentDirectoryRow
{
    public Guid Id { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int MajorId { get; init; }
    public string MajorCode { get; init; } = string.Empty;
    public string MajorName { get; init; } = string.Empty;
    public int? SpecializationId { get; init; }
    public string SpecializationCode { get; init; } = string.Empty;
    public string SpecializationName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public bool IsSeekingTeam { get; init; }
}

public interface IStudentRepository
{
    /// <summary>Sinh viên có trong danh sách học kỳ, theo bộ lọc. Tìm theo họ tên hoặc MSSV, không phân biệt hoa thường.</summary>
    Task<(IReadOnlyList<StudentDirectoryRow> Items, int TotalCount)> ListDirectoryAsync(StudentDirectoryFilter filter, CancellationToken ct);

    /// <summary>Sinh viên theo id; id không tồn tại thì không có trong kết quả.</summary>
    Task<IReadOnlyList<StudentSnapshotRow>> LookupAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Sinh viên theo MSSV (đã viết hoa); MSSV không tồn tại thì không có trong kết quả.</summary>
    Task<IReadOnlyList<StudentSnapshotRow>> LookupByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct);

    /// <summary>Mọi sinh viên trong danh sách học kỳ, lọc theo môn nếu có; sắp theo MSSV.</summary>
    Task<IReadOnlyList<StudentSnapshotRow>> ListEnrolledAsync(Guid semesterId, string? courseCode, CancellationToken ct);

    /// <summary>Sinh viên trong danh sách học kỳ (lọc theo môn nếu có) đang bật cờ tìm nhóm; sắp theo MSSV.</summary>
    Task<IReadOnlyList<SeekingStudentRow>> ListSeekingAsync(Guid semesterId, string? courseCode, CancellationToken ct);
}

public sealed class StudentRepository(AcademicDbContext db) : IStudentRepository
{
    public async Task<(IReadOnlyList<StudentDirectoryRow> Items, int TotalCount)> ListDirectoryAsync(StudentDirectoryFilter filter, CancellationToken ct)
    {
        var query = from e in db.SemesterEnrollments.AsNoTracking()
                    join s in db.Students on e.StudentId equals s.Id
                    join c in db.ClassCodes on e.ClassCodeId equals c.Id
                    join m in db.Majors on s.MajorId equals m.Id
                    join sp in db.Specializations on s.SpecializationId equals sp.Id into spGroup
                    from sp in spGroup.DefaultIfEmpty()
                    where e.SemesterId == filter.SemesterId
                    select new StudentDirectoryRow
                    {
                        Id = s.Id, StudentCode = s.StudentCode, FullName = s.FullName, Email = s.Email,
                        MajorId = s.MajorId, MajorCode = m.Code, MajorName = m.Name,
                        SpecializationId = s.SpecializationId, SpecializationCode = sp != null ? sp.Code : string.Empty,
                        SpecializationName = sp != null ? sp.Name : string.Empty,
                        ClassCode = c.Code, CourseCode = e.CourseCode, IsSeekingTeam = s.IsSeekingTeam,
                    };
        if (filter.CourseCode is not null)
        {
            query = query.Where(r => r.CourseCode == filter.CourseCode);
        }
        if (!string.IsNullOrWhiteSpace(filter.ClassCode))
        {
            var classCode = filter.ClassCode.Trim().ToUpperInvariant();
            query = query.Where(r => r.ClassCode == classCode);
        }
        if (filter.SpecializationId is { } specializationId)
        {
            query = query.Where(r => r.SpecializationId == specializationId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(r => EF.Functions.ILike(r.FullName, pattern) || EF.Functions.ILike(r.StudentCode, pattern));
        }

        var total = await query.CountAsync(ct);
        var ordered = filter.Sort switch
        {
            "-name" => query.OrderByDescending(r => r.FullName),
            "studentCode" => query.OrderBy(r => r.StudentCode),
            "-studentCode" => query.OrderByDescending(r => r.StudentCode),
            _ => query.OrderBy(r => r.FullName),
        };
        var items = await ordered.ThenBy(r => r.Id).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<IReadOnlyList<StudentSnapshotRow>> LookupAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        SnapshotsAsync(db.Students.Where(s => studentIds.Contains(s.Id)), semesterId, ct);

    public Task<IReadOnlyList<StudentSnapshotRow>> LookupByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct) =>
        SnapshotsAsync(db.Students.Where(s => studentCodes.Contains(s.StudentCode)), semesterId, ct);

    public Task<IReadOnlyList<StudentSnapshotRow>> ListEnrolledAsync(Guid semesterId, string? courseCode, CancellationToken ct) =>
        SnapshotsAsync(db.Students.Where(s => db.SemesterEnrollments.Any(e => e.StudentId == s.Id && e.SemesterId == semesterId
            && (courseCode == null || e.CourseCode == courseCode))), semesterId, ct);

    public async Task<IReadOnlyList<SeekingStudentRow>> ListSeekingAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var students = await db.Students.AsNoTracking()
            .Where(s => s.IsSeekingTeam && db.SemesterEnrollments.Any(e => e.StudentId == s.Id && e.SemesterId == semesterId
                && (courseCode == null || e.CourseCode == courseCode)))
            .OrderBy(s => s.StudentCode)
            .Select(s => new { s.Id, s.StudentCode, s.FullName, s.SpecializationId, s.MajorId, s.Description })
            .ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var skills = (await db.StudentSkills.AsNoTracking().Where(k => ids.Contains(k.StudentId)).ToListAsync(ct))
            .ToLookup(k => k.StudentId);
        return students.Select(s => new SeekingStudentRow(s.Id, s.StudentCode, s.FullName, s.SpecializationId, s.MajorId, s.Description,
            skills[s.Id].Select(k => (k.Skill, k.Level)).ToList())).ToList();
    }

    /// <summary>
    /// Một câu SQL: sinh viên kèm môn và mã lớp trong học kỳ (left join trên <c>uq_semester_enrollments_semester_student</c>,
    /// mỗi sinh viên tối đa một dòng mỗi học kỳ).
    /// </summary>
    private async Task<IReadOnlyList<StudentSnapshotRow>> SnapshotsAsync(IQueryable<Student> students, Guid? semesterId, CancellationToken ct) =>
        await (from s in students.AsNoTracking()
               join e in db.SemesterEnrollments.Where(e => e.SemesterId == semesterId) on s.Id equals e.StudentId into enrollments
               from e in enrollments.DefaultIfEmpty()
               join c in db.ClassCodes on e.ClassCodeId equals c.Id into classes
               from c in classes.DefaultIfEmpty()
               orderby s.StudentCode
               select new StudentSnapshotRow(s.Id, s.StudentCode, s.FullName, s.SpecializationId, e.CourseCode, c.Code, s.MajorId))
            .ToListAsync(ct);
}
