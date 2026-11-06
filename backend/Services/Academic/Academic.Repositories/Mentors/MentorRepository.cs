using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Mentors;

/// <summary>Domain của một mentor (đã nối bảng <c>domains</c>).</summary>
public sealed record MentorDomainRow(Guid MentorId, int Id, string Code, string Name);

/// <summary>Mentor rút gọn để lọc theo số nhóm (<c>hasCapacity</c>) trước khi phân trang.</summary>
public sealed record MentorCapacityRow(Guid Id, int MaxTeams);

/// <summary>Bộ lọc danh sách mentor.</summary>
public sealed record MentorFilter(string? Search, int? DomainId, bool? IsInternal, int Page, int PageSize);

public interface IMentorRepository
{
    /// <summary>Tìm theo họ tên, email hoặc tổ chức (không phân biệt hoa thường); sắp theo họ tên.</summary>
    Task<(IReadOnlyList<Mentor> Items, int TotalCount)> ListAsync(MentorFilter filter, CancellationToken ct);

    /// <summary>
    /// Mọi mentor khớp bộ lọc (bỏ qua trang), cùng thứ tự với <see cref="ListAsync"/>: dùng khi lọc theo số nhóm của Team
    /// trên toàn bộ dữ liệu rồi mới phân trang.
    /// </summary>
    Task<IReadOnlyList<MentorCapacityRow>> ListCapacityRowsAsync(MentorFilter filter, CancellationToken ct);

    /// <summary>Mentor theo danh sách id, giữ đúng thứ tự của <paramref name="ids"/>.</summary>
    Task<IReadOnlyList<Mentor>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Mentor?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<Mentor?> FindByEmailAsync(string email, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct);

    /// <summary>Email đã thuộc một giảng viên hoặc sinh viên chưa (không phân biệt hoa thường).</summary>
    Task<bool> EmailUsedByPersonAsync(string email, CancellationToken ct);

    /// <summary>Hồ sơ mentor gắn với giảng viên. Có theo dõi thay đổi.</summary>
    Task<Mentor?> FindByLecturerAsync(Guid lecturerId, CancellationToken ct);

    /// <summary>Giảng viên đã gắn với mentor khác chưa (mỗi giảng viên một mentor).</summary>
    Task<bool> LecturerLinkedAsync(Guid lecturerId, Guid? exceptId, CancellationToken ct);

    /// <summary>Domain của các mentor, sắp theo mã domain.</summary>
    Task<IReadOnlyList<MentorDomainRow>> ListDomainsAsync(IReadOnlyCollection<Guid> mentorIds, CancellationToken ct);

    void Add(Mentor mentor);

    /// <summary>Thay toàn bộ domain của mentor; lưu cùng lần <see cref="SaveAsync"/>.</summary>
    Task ReplaceDomainsAsync(Guid mentorId, IReadOnlyCollection<int> domainIds, CancellationToken ct);

    /// <summary>Ghi sự kiện vào outbox (topic <c>exe.student</c>, key = id mentor); lưu cùng lần <see cref="SaveAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid mentorId, TPayload payload);

    Task SaveAsync(CancellationToken ct);
}

public sealed class MentorRepository(AcademicDbContext db) : IMentorRepository
{
    public async Task<(IReadOnlyList<Mentor> Items, int TotalCount)> ListAsync(MentorFilter filter, CancellationToken ct)
    {
        var query = Filtered(filter);
        var total = await query.CountAsync(ct);
        var items = await Ordered(query).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<MentorCapacityRow>> ListCapacityRowsAsync(MentorFilter filter, CancellationToken ct) =>
        await Ordered(Filtered(filter)).Select(m => new MentorCapacityRow(m.Id, m.MaxTeams)).ToListAsync(ct);

    public async Task<IReadOnlyList<Mentor>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var byId = await db.Mentors.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    private IQueryable<Mentor> Filtered(MentorFilter filter)
    {
        var query = db.Mentors.AsNoTracking();
        if (filter.IsInternal is { } isInternal)
        {
            query = query.Where(m => m.IsInternal == isInternal);
        }
        if (filter.DomainId is { } domainId)
        {
            query = query.Where(m => db.MentorDomains.Any(d => d.MentorId == m.Id && d.DomainId == domainId));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(m => EF.Functions.ILike(m.FullName, pattern) || EF.Functions.ILike(m.Email, pattern)
                || EF.Functions.ILike(m.Organization!, pattern));
        }
        return query;
    }

    private static IOrderedQueryable<Mentor> Ordered(IQueryable<Mentor> query) => query.OrderBy(m => m.FullName).ThenBy(m => m.Id);

    public Task<Mentor?> FindAsync(Guid id, CancellationToken ct) => db.Mentors.SingleOrDefaultAsync(m => m.Id == id, ct);

    public Task<Mentor?> FindByEmailAsync(string email, CancellationToken ct) => db.Mentors.SingleOrDefaultAsync(m => m.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct) =>
        db.Mentors.AnyAsync(m => m.Email == email && m.Id != exceptId, ct);

    public async Task<bool> EmailUsedByPersonAsync(string email, CancellationToken ct) =>
        await db.Lecturers.AnyAsync(l => l.Email == email, ct) || await db.Students.AnyAsync(s => s.Email == email, ct);

    public Task<Mentor?> FindByLecturerAsync(Guid lecturerId, CancellationToken ct) =>
        db.Mentors.SingleOrDefaultAsync(m => m.LecturerId == lecturerId, ct);

    public Task<bool> LecturerLinkedAsync(Guid lecturerId, Guid? exceptId, CancellationToken ct) =>
        db.Mentors.AnyAsync(m => m.LecturerId == lecturerId && m.Id != exceptId, ct);

    public async Task<IReadOnlyList<MentorDomainRow>> ListDomainsAsync(IReadOnlyCollection<Guid> mentorIds, CancellationToken ct) =>
        await (from md in db.MentorDomains.AsNoTracking()
               join d in db.Domains on md.DomainId equals d.Id
               where mentorIds.Contains(md.MentorId)
               orderby d.Code
               select new MentorDomainRow(md.MentorId, d.Id, d.Code, d.Name))
            .ToListAsync(ct);

    public void Add(Mentor mentor) => db.Mentors.Add(mentor);

    public async Task ReplaceDomainsAsync(Guid mentorId, IReadOnlyCollection<int> domainIds, CancellationToken ct)
    {
        var current = await db.MentorDomains.Where(md => md.MentorId == mentorId).ToListAsync(ct);
        db.MentorDomains.RemoveRange(current.Where(md => !domainIds.Contains(md.DomainId)));
        db.MentorDomains.AddRange(domainIds.Where(id => current.All(md => md.DomainId != id))
            .Select(id => new MentorDomain { MentorId = mentorId, DomainId = id }));
    }

    public void AddEvent<TPayload>(string eventType, Guid mentorId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Student, eventType, "Mentor", mentorId, payload);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
