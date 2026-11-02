using Academic.Repositories.Context;
using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Academic.Repositories.Mentors;

/// <summary>Domain của một mentor (đã nối bảng <c>domains</c>).</summary>
public sealed record MentorDomainRow(Guid MentorId, int Id, string Code, string Name);

/// <summary>Bộ lọc danh sách mentor.</summary>
public sealed record MentorFilter(string? Search, int? DomainId, bool? IsInternal, int Page, int PageSize);

public interface IMentorRepository
{
    /// <summary>Tìm theo họ tên, email hoặc tổ chức (không phân biệt hoa thường); sắp theo họ tên.</summary>
    Task<(IReadOnlyList<Mentor> Items, int TotalCount)> ListAsync(MentorFilter filter, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi, để sửa rồi gọi <see cref="SaveAsync"/>.</summary>
    Task<Mentor?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Có theo dõi thay đổi.</summary>
    Task<Mentor?> FindByEmailAsync(string email, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct);

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
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(m => m.FullName).ThenBy(m => m.Id).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<Mentor?> FindAsync(Guid id, CancellationToken ct) => db.Mentors.SingleOrDefaultAsync(m => m.Id == id, ct);

    public Task<Mentor?> FindByEmailAsync(string email, CancellationToken ct) => db.Mentors.SingleOrDefaultAsync(m => m.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId, CancellationToken ct) =>
        db.Mentors.AnyAsync(m => m.Email == email && m.Id != exceptId, ct);

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
