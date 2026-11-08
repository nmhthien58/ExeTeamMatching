using Academic.Repositories.Catalogs;
using Academic.Repositories.Context;
using Academic.Repositories.Semesters;
using Academic.Repositories.Students;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Imports;

public interface IImportRepository
{
    Task<Semester?> FindSemesterAsync(Guid id, CancellationToken ct);

    /// <summary>Mọi ngành / tiền tố ngành (BE-91).</summary>
    Task<IReadOnlyList<Major>> ListMajorsAsync(CancellationToken ct);

    /// <summary>Mọi chuyên ngành (cả ngừng dùng, để báo lỗi rõ hơn "không tồn tại").</summary>
    Task<IReadOnlyList<Specialization>> ListSpecializationsAsync(CancellationToken ct);

    Task<IReadOnlyList<ClassCode>> ListClassCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct);

    /// <summary>Sinh viên có MSSV hoặc email trong danh sách. Có theo dõi thay đổi, để cập nhật rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<IReadOnlyList<Student>> FindStudentsAsync(IReadOnlyCollection<string> studentCodes, IReadOnlyCollection<string> emails, CancellationToken ct);

    /// <summary>Các email trong danh sách đã thuộc một mentor.</summary>
    Task<IReadOnlyList<string>> FindMentorEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct);

    /// <summary>Dòng danh sách học kỳ của các sinh viên. Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<SemesterEnrollment>> FindEnrollmentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Lịch sử import, mới nhất trước.</summary>
    Task<(IReadOnlyList<ImportBatch> Items, int TotalCount)> ListBatchesAsync(Guid? semesterId, int page, int pageSize, CancellationToken ct);

    void Add(ImportBatch batch);

    void Add(Student student);

    void Add(SemesterEnrollment enrollment);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.student</c>, key = id sinh viên); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddStudentEvent<TPayload>(string eventType, Guid studentId, TPayload payload);

    Task SaveAsync(CancellationToken ct);
}

public sealed class ImportRepository(AcademicDbContext db) : IImportRepository
{
    public Task<Semester?> FindSemesterAsync(Guid id, CancellationToken ct) => db.Semesters.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Major>> ListMajorsAsync(CancellationToken ct) =>
        await db.Majors.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<Specialization>> ListSpecializationsAsync(CancellationToken ct) =>
        await db.Specializations.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<ClassCode>> ListClassCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct) =>
        await db.ClassCodes.AsNoTracking().Where(c => codes.Contains(c.Code)).ToListAsync(ct);

    /// <remarks><c>email</c> là citext nên so sánh trong SQL không phân biệt hoa thường.</remarks>
    public async Task<IReadOnlyList<Student>> FindStudentsAsync(IReadOnlyCollection<string> studentCodes, IReadOnlyCollection<string> emails, CancellationToken ct) =>
        await db.Students.Where(s => studentCodes.Contains(s.StudentCode) || emails.Contains(s.Email)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> FindMentorEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct) =>
        await db.Mentors.AsNoTracking().Where(m => emails.Contains(m.Email)).Select(m => m.Email).ToListAsync(ct);

    public async Task<IReadOnlyList<SemesterEnrollment>> FindEnrollmentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        await db.SemesterEnrollments.Where(e => e.SemesterId == semesterId && studentIds.Contains(e.StudentId)).ToListAsync(ct);

    public async Task<(IReadOnlyList<ImportBatch> Items, int TotalCount)> ListBatchesAsync(Guid? semesterId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ImportBatches.AsNoTracking();
        if (semesterId is { } id)
        {
            query = query.Where(b => b.SemesterId == id);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public void Add(ImportBatch batch) => db.ImportBatches.Add(batch);

    public void Add(Student student) => db.Students.Add(student);

    public void Add(SemesterEnrollment enrollment) => db.SemesterEnrollments.Add(enrollment);

    public void AddStudentEvent<TPayload>(string eventType, Guid studentId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Student, eventType, "Student", studentId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

/// <summary>Giá trị cột <c>status</c> (CHECK <c>ck_import_batches_status</c>).</summary>
public static class ImportStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}
