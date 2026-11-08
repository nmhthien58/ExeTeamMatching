using Academic.Repositories.Catalogs;
using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Lecturers;

/// <summary>Domain chuyên môn của một giảng viên (đã nối bảng <c>domains</c>).</summary>
public sealed record LecturerDomainRow(Guid LecturerId, int Id, string Code, string Name);

public interface ILecturerRepository
{
    /// <summary>Tìm theo họ tên hoặc email (không phân biệt hoa thường); sắp theo họ tên.</summary>
    Task<(IReadOnlyList<Lecturer> Items, int TotalCount)> ListAsync(string? search, bool activeOnly, int page, int pageSize, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Lecturer?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<Lecturer?> FindByEmailAsync(string email, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct);

    /// <summary>Tìm các email đã tồn tại trong DB trong tập email truyền vào (so sánh không phân biệt hoa thường).</summary>
    Task<HashSet<string>> FindExistingEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct);

    /// <summary>Domain chuyên môn của các giảng viên, sắp theo mã domain.</summary>
    Task<IReadOnlyList<LecturerDomainRow>> ListDomainsAsync(IReadOnlyCollection<Guid> lecturerIds, CancellationToken ct);

    /// <summary>Domain trong danh mục có id thuộc danh sách.</summary>
    Task<IReadOnlyList<Domain>> FindCatalogDomainsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    void Add(Lecturer lecturer);

    /// <summary>Thay toàn bộ domain chuyên môn của giảng viên; lưu cùng lần <see cref="SaveAsync"/>.</summary>
    Task ReplaceDomainsAsync(Guid lecturerId, IReadOnlyCollection<int> domainIds, CancellationToken ct);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.student</c>, key = id giảng viên); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid lecturerId, TPayload payload);

    Task SaveAsync(CancellationToken ct);
}

public sealed class LecturerRepository(AcademicDbContext db) : ILecturerRepository
{
    public async Task<(IReadOnlyList<Lecturer> Items, int TotalCount)> ListAsync(string? search, bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Lecturers.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(l => l.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(l => EF.Functions.ILike(l.FullName, pattern) || EF.Functions.ILike(l.Email, pattern));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(l => l.FullName).ThenBy(l => l.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<Lecturer?> FindAsync(Guid id, CancellationToken ct) => db.Lecturers.SingleOrDefaultAsync(l => l.Id == id, ct);

    public Task<Lecturer?> FindByEmailAsync(string email, CancellationToken ct) => db.Lecturers.SingleOrDefaultAsync(l => l.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct) =>
        db.Lecturers.AnyAsync(l => l.Email == email && l.Id != exceptId, ct);

    public async Task<HashSet<string>> FindExistingEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken ct)
    {
        if (emails.Count == 0) return [];
        var found = await db.Lecturers.AsNoTracking()
            .Where(l => emails.Contains(l.Email))
            .Select(l => l.Email)
            .ToListAsync(ct);
        return found.Select(e => e.ToLowerInvariant()).ToHashSet();
    }

    public async Task<IReadOnlyList<LecturerDomainRow>> ListDomainsAsync(IReadOnlyCollection<Guid> lecturerIds, CancellationToken ct) =>
        await (from ld in db.LecturerDomains.AsNoTracking()
               join d in db.Domains on ld.DomainId equals d.Id
               where lecturerIds.Contains(ld.LecturerId)
               orderby d.Code
               select new LecturerDomainRow(ld.LecturerId, d.Id, d.Code, d.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Domain>> FindCatalogDomainsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Domains.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync(ct);

    public void Add(Lecturer lecturer) => db.Lecturers.Add(lecturer);

    public async Task ReplaceDomainsAsync(Guid lecturerId, IReadOnlyCollection<int> domainIds, CancellationToken ct)
    {
        var current = await db.LecturerDomains.Where(ld => ld.LecturerId == lecturerId).ToListAsync(ct);
        db.LecturerDomains.RemoveRange(current.Where(ld => !domainIds.Contains(ld.DomainId)));
        db.LecturerDomains.AddRange(domainIds.Where(id => current.All(ld => ld.DomainId != id))
            .Select(id => new LecturerDomain { LecturerId = lecturerId, DomainId = id }));
    }

    public void AddEvent<TPayload>(string eventType, Guid lecturerId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Student, eventType, "Lecturer", lecturerId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
