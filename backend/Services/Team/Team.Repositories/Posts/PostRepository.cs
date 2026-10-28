using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Members;

namespace Team.Repositories.Posts;

public static class PostStatuses
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
}

public interface IPostRepository
{
    /// <summary>Bài tuyển đang mở của nhóm (được theo dõi).</summary>
    Task<RecruitmentPost?> GetOpenRecruitmentAsync(Guid teamId, CancellationToken ct);

    Task<List<int>> ListSpecializationIdsAsync(Guid recruitmentPostId, CancellationToken ct);

    /// <summary>Thay danh sách chuyên ngành cần tuyển (chỉ thêm / bớt phần khác).</summary>
    Task ReplaceSpecializationsAsync(Guid recruitmentPostId, IReadOnlyCollection<int> specializationIds, CancellationToken ct);

    /// <summary>Bài tìm nhóm đang mở của sinh viên trong học kỳ (được theo dõi).</summary>
    Task<SeekingPost?> GetOpenSeekingAsync(Guid semesterId, Guid studentId, CancellationToken ct);

    Task<ILookup<Guid, SeekingPostDomain>> ListDomainsAsync(IReadOnlyCollection<Guid> seekingPostIds, CancellationToken ct);

    /// <summary>Thay domain quan tâm (chỉ thêm / bớt phần khác).</summary>
    Task ReplaceDomainsAsync(Guid seekingPostId, IReadOnlyCollection<(int Id, string Code)> domains, CancellationToken ct);

    /// <summary>S10 – bài đang mở của sinh viên chưa có nhóm trong học kỳ, mới cập nhật trước.</summary>
    Task<(List<SeekingPost> Items, int TotalCount)> SearchSeekingAsync(
        Guid semesterId, int? specializationId, string? domainCode, int page, int pageSize, CancellationToken ct);

    void Add(object entity);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class PostRepository(TeamDbContext db) : IPostRepository
{
    public Task<RecruitmentPost?> GetOpenRecruitmentAsync(Guid teamId, CancellationToken ct) =>
        db.Set<RecruitmentPost>().FirstOrDefaultAsync(p => p.TeamId == teamId && p.Status == PostStatuses.Open, ct);

    public Task<List<int>> ListSpecializationIdsAsync(Guid recruitmentPostId, CancellationToken ct) =>
        db.Set<RecruitmentPostSpecialization>().AsNoTracking().Where(s => s.RecruitmentPostId == recruitmentPostId)
            .OrderBy(s => s.SpecializationId).Select(s => s.SpecializationId).ToListAsync(ct);

    public async Task ReplaceSpecializationsAsync(Guid recruitmentPostId, IReadOnlyCollection<int> specializationIds, CancellationToken ct)
    {
        var current = await db.Set<RecruitmentPostSpecialization>().Where(s => s.RecruitmentPostId == recruitmentPostId).ToListAsync(ct);
        db.RemoveRange(current.Where(s => !specializationIds.Contains(s.SpecializationId)));
        db.AddRange(specializationIds.Except(current.Select(s => s.SpecializationId))
            .Select(id => new RecruitmentPostSpecialization { RecruitmentPostId = recruitmentPostId, SpecializationId = id }));
    }

    public Task<SeekingPost?> GetOpenSeekingAsync(Guid semesterId, Guid studentId, CancellationToken ct) =>
        db.Set<SeekingPost>().FirstOrDefaultAsync(p => p.SemesterId == semesterId && p.StudentId == studentId && p.Status == PostStatuses.Open, ct);

    public async Task<ILookup<Guid, SeekingPostDomain>> ListDomainsAsync(IReadOnlyCollection<Guid> seekingPostIds, CancellationToken ct) =>
        (await db.Set<SeekingPostDomain>().AsNoTracking().Where(d => seekingPostIds.Contains(d.SeekingPostId)).OrderBy(d => d.DomainCode).ToListAsync(ct))
            .ToLookup(d => d.SeekingPostId);

    public async Task ReplaceDomainsAsync(Guid seekingPostId, IReadOnlyCollection<(int Id, string Code)> domains, CancellationToken ct)
    {
        var current = await db.Set<SeekingPostDomain>().Where(d => d.SeekingPostId == seekingPostId).ToListAsync(ct);
        var wanted = domains.Select(d => d.Id).ToList();
        db.RemoveRange(current.Where(d => !wanted.Contains(d.DomainId)));
        db.AddRange(domains.Where(d => current.All(c => c.DomainId != d.Id))
            .Select(d => new SeekingPostDomain { SeekingPostId = seekingPostId, DomainId = d.Id, DomainCode = d.Code }));
    }

    public async Task<(List<SeekingPost> Items, int TotalCount)> SearchSeekingAsync(
        Guid semesterId, int? specializationId, string? domainCode, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<SeekingPost>().AsNoTracking().Where(p => p.SemesterId == semesterId && p.Status == PostStatuses.Open
            && !db.Set<TeamMember>().Any(m => m.SemesterId == semesterId && m.StudentId == p.StudentId));
        if (specializationId is not null)
        {
            query = query.Where(p => p.SpecializationId == specializationId);
        }
        if (domainCode is not null)
        {
            query = query.Where(p => db.Set<SeekingPostDomain>().Any(d => d.SeekingPostId == p.Id && d.DomainCode == domainCode));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public void Add(object entity) => db.Add(entity);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
