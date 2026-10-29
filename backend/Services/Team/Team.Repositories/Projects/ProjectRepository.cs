using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;

namespace Team.Repositories.Projects;

public sealed record ProjectSearch(Guid? SemesterId, string? Search, string? Domain, bool PivotedOnly, int Page, int PageSize);
public sealed record ProjectAdvisors(string? InstructorName, string? MentorName);

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken ct);
    Task<List<Project>> ListAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<List<ProjectVersion>> VersionsAsync(Guid id, CancellationToken ct);
    Task<List<ProjectVersion>> VersionsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<List<Guid>> TeamIdsAsync(Guid id, CancellationToken ct);
    Task<List<Guid>> TeamIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<(List<Guid> Ids, int Total)> SearchAsync(ProjectSearch search, CancellationToken ct);
    Task<List<Guid>> UnlinkedTeamIdsAsync(Guid semesterId, CancellationToken ct);
    Task<TeamEntity?> GetTeamForUpdateAsync(Guid id, CancellationToken ct);
    Task<TeamEntity?> SourceTeamAsync(Project project, CancellationToken ct);
    Task<bool> AlreadyLinkedAsync(Guid projectId, Guid sourceTeamId, Guid semesterId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, ProjectAdvisors>> AdvisorHistoryAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class ProjectRepository(TeamDbContext db) : IProjectRepository
{
    public Task<Project?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Project>> ListAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        db.Set<Project>().AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public Task<List<ProjectVersion>> VersionsAsync(Guid id, CancellationToken ct) =>
        db.Set<ProjectVersion>().AsNoTracking().Where(v => v.ProjectId == id)
            .OrderByDescending(v => v.Version).ToListAsync(ct);

    public Task<List<ProjectVersion>> VersionsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        db.Set<ProjectVersion>().AsNoTracking().Where(v => ids.Contains(v.ProjectId)).ToListAsync(ct);

    public Task<List<Guid>> TeamIdsAsync(Guid id, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking().Where(t => t.ProjectId == id)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).Select(t => t.Id).ToListAsync(ct);

    public Task<List<Guid>> TeamIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking().Where(t => t.ProjectId != null && ids.Contains(t.ProjectId.Value))
            .Select(t => t.Id).ToListAsync(ct);

    public async Task<(List<Guid> Ids, int Total)> SearchAsync(ProjectSearch s, CancellationToken ct)
    {
        var query = db.Set<Project>().AsNoTracking().AsQueryable();
        if (s.SemesterId is { } semesterId)
            query = query.Where(p => db.Set<TeamEntity>().Any(t => t.ProjectId == p.Id && t.SemesterId == semesterId && t.Status != "DISBANDED"));
        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var pattern = "%" + s.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
        }
        if (s.Domain is { } domain)
            query = query.Where(p => db.Set<TeamEntity>().Any(t => t.ProjectId == p.Id &&
                db.Set<TeamDomain>().Any(d => d.TeamId == t.Id && d.DomainCode == domain)));
        if (s.PivotedOnly)
            query = query.Where(p => db.Set<ProjectVersion>().Any(v => v.ProjectId == p.Id && v.ChangeType == "PIVOT"));

        var total = await query.CountAsync(ct);
        var ids = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
            .Skip((s.Page - 1) * s.PageSize).Take(s.PageSize).Select(p => p.Id).ToListAsync(ct);
        return (ids, total);
    }

    public Task<List<Guid>> UnlinkedTeamIdsAsync(Guid semesterId, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.CourseCode == "EXE201" && t.IsInherited &&
                t.ProjectId == null && t.SourceTeamId == null && t.Status != "DISBANDED")
            .OrderBy(t => t.Name).ThenBy(t => t.Id).Select(t => t.Id).ToListAsync(ct);

    public Task<TeamEntity?> GetTeamForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Set<TeamEntity>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TeamEntity?> SourceTeamAsync(Project project, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.ProjectId == project.Id && t.SemesterId == project.OriginSemesterId &&
                t.CourseCode == "EXE101")
            .OrderBy(t => t.Status == "DISBANDED").ThenBy(t => t.CreatedAt).FirstOrDefaultAsync(ct);

    public Task<bool> AlreadyLinkedAsync(Guid projectId, Guid sourceTeamId, Guid semesterId, CancellationToken ct) =>
        db.Set<TeamEntity>().AnyAsync(t => t.SourceTeamId == sourceTeamId ||
            (t.Status != "DISBANDED" && t.SemesterId == semesterId && t.ProjectId == projectId), ct);

    public async Task<IReadOnlyDictionary<Guid, ProjectAdvisors>> AdvisorHistoryAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var instructors = (await db.Set<LecturerAssignment>().AsNoTracking()
            .Where(a => teamIds.Contains(a.TeamId)).ToListAsync(ct))
            .GroupBy(a => a.TeamId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(a => a.Status == "ASSIGNED").ThenByDescending(a => a.AssignedAt)
                    .First().LecturerName);
        var mentors = (await db.Set<MentorAssignment>().AsNoTracking()
            .Where(a => teamIds.Contains(a.TeamId)).ToListAsync(ct))
            .GroupBy(a => a.TeamId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(a => a.Status == "ASSIGNED").ThenByDescending(a => a.AssignedAt)
                    .First().MentorName);
        return teamIds.ToDictionary(id => id, id => new ProjectAdvisors(
            instructors.GetValueOrDefault(id), mentors.GetValueOrDefault(id)));
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
