using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Teams;

namespace Team.Repositories.Export;

/// <summary>Truy vấn của module Export (BE-50).</summary>
public interface IExportRepository
{
    /// <summary>Nhóm cần xuất, theo môn rồi tên; không lọc trạng thái thì bỏ nhóm đã giải tán.</summary>
    Task<List<Guid>> ListTeamIdsAsync(Guid semesterId, string? courseCode, string? status, CancellationToken ct);
}

public sealed class ExportRepository(TeamDbContext db) : IExportRepository
{
    public Task<List<Guid>> ListTeamIdsAsync(Guid semesterId, string? courseCode, string? status, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && (courseCode == null || t.CourseCode == courseCode)
                && (status == null ? t.Status != "DISBANDED" : t.Status == status))
            .OrderBy(t => t.CourseCode).ThenBy(t => t.Name)
            .Select(t => t.Id)
            .ToListAsync(ct);
}
