using Academic.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Semesters;

public interface ISemesterRepository
{
    /// <summary>Học kỳ mới nhất trước.</summary>
    Task<IReadOnlyList<Semester>> ListAsync(CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Semester?> FindAsync(Guid id, CancellationToken ct);

    Task<Semester?> GetCurrentAsync(CancellationToken ct);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct);

    void Add(Semester semester);

    /// <summary>
    /// Lưu thay đổi. Học kỳ có <c>IsCurrent</c> thì bỏ cờ của học kỳ khác trước, trong cùng transaction,
    /// vì unique index một phần <c>uq_semesters_is_current</c> chỉ cho một học kỳ hiện hành.
    /// </summary>
    Task SaveAsync(Semester semester, CancellationToken ct);
}

public sealed class SemesterRepository(AcademicDbContext db) : ISemesterRepository
{
    public async Task<IReadOnlyList<Semester>> ListAsync(CancellationToken ct) =>
        await db.Semesters.AsNoTracking().OrderByDescending(s => s.StartDate).ToListAsync(ct);

    public Task<Semester?> FindAsync(Guid id, CancellationToken ct) => db.Semesters.SingleOrDefaultAsync(s => s.Id == id, ct);

    public Task<Semester?> GetCurrentAsync(CancellationToken ct) => db.Semesters.AsNoTracking().SingleOrDefaultAsync(s => s.IsCurrent, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => db.Semesters.AnyAsync(s => s.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) =>
        db.Semesters.AnyAsync(s => s.Code == code && s.Id != exceptId, ct);

    public void Add(Semester semester) => db.Semesters.Add(semester);

    public async Task SaveAsync(Semester semester, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (semester.IsCurrent)
        {
            await db.Semesters.Where(s => s.IsCurrent && s.Id != semester.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false), ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
