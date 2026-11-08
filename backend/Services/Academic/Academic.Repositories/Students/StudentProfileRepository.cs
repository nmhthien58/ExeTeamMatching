using Academic.Repositories.Catalogs;
using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Students;

/// <summary>Môn và mã lớp của sinh viên trong học kỳ hiện hành (null khi không có trong danh sách học kỳ).</summary>
public sealed record CurrentEnrollmentRow(string CourseCode, string ClassCode);

public interface IStudentProfileRepository
{
    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Student?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi. Email là citext nên không phân biệt hoa thường.</summary>
    Task<Student?> FindByEmailAsync(string email, CancellationToken ct);

    Task<Specialization?> FindSpecializationAsync(int id, CancellationToken ct);

    Task<Major?> FindMajorAsync(int id, CancellationToken ct);

    Task<CurrentEnrollmentRow?> FindCurrentEnrollmentAsync(Guid studentId, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<IReadOnlyList<StudentSkill>> ListSkillsAsync(Guid studentId, CancellationToken ct);

    void AddSkill(StudentSkill skill);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.student</c>, key = id sinh viên); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid studentId, TPayload payload);

    Task SaveAsync(CancellationToken ct);
}

public sealed class StudentProfileRepository(AcademicDbContext db) : IStudentProfileRepository
{
    public Task<Student?> FindAsync(Guid id, CancellationToken ct) => db.Students.SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<Student?> FindByEmailAsync(string email, CancellationToken ct) => db.Students.SingleOrDefaultAsync(s => s.Email == email, ct);

    public Task<Specialization?> FindSpecializationAsync(int id, CancellationToken ct) =>
        db.Specializations.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<Major?> FindMajorAsync(int id, CancellationToken ct) =>
        db.Majors.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);

    public Task<CurrentEnrollmentRow?> FindCurrentEnrollmentAsync(Guid studentId, CancellationToken ct) =>
        (from e in db.SemesterEnrollments.AsNoTracking()
         join s in db.Semesters on e.SemesterId equals s.Id
         join c in db.ClassCodes on e.ClassCodeId equals c.Id
         where e.StudentId == studentId && s.IsCurrent
         select new CurrentEnrollmentRow(e.CourseCode, c.Code))
        .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StudentSkill>> ListSkillsAsync(Guid studentId, CancellationToken ct) =>
        await db.StudentSkills.Where(s => s.StudentId == studentId).ToListAsync(ct);

    public void AddSkill(StudentSkill skill) => db.StudentSkills.Add(skill);

    public void AddEvent<TPayload>(string eventType, Guid studentId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Student, eventType, "Student", studentId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
