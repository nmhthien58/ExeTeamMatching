using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Teams;

namespace Team.Repositories.MentorAssignments;

public sealed class MentorAssignmentRepository(TeamDbContext db) : IMentorAssignmentRepository
{
    public Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) =>
        db.Set<MentorAssignment>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);

    public Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct) =>
        db.Set<MentorAssignment>().AsNoTracking()
            .Where(a => a.MentorId == mentorId && a.Status == "ASSIGNED")
            .ToListAsync(ct);

    public Task<List<MentorAssignment>> ListActiveByMentorIdTrackedAsync(Guid mentorId, CancellationToken ct) =>
        db.Set<MentorAssignment>()
            .Where(a => a.MentorId == mentorId && a.Status == "ASSIGNED")
            .ToListAsync(ct);

    /// <summary>
    /// Số nhóm đang mentor (dòng ASSIGNED, nhóm chưa giải tán) – cùng điều kiện với <c>AdvisorMatchingRepository.CountMentorLoadsAsync</c>;
    /// có <paramref name="semesterId"/> thì chỉ tính nhóm của học kỳ đó.
    /// </summary>
    public async Task<Dictionary<Guid, int>> CountActiveTeamsByMentorIdsAsync(IReadOnlyList<Guid> mentorIds, Guid? semesterId, CancellationToken ct)
    {
        if (mentorIds.Count == 0) return [];

        var query = from assignment in db.Set<MentorAssignment>()
                    join team in db.Set<TeamEntity>() on assignment.TeamId equals team.Id
                    where mentorIds.Contains(assignment.MentorId) && assignment.Status == "ASSIGNED" && team.Status != "DISBANDED"
                    select new { assignment.MentorId, team.SemesterId };

        if (semesterId is { } sid)
        {
            query = query.Where(x => x.SemesterId == sid);
        }

        return await query
            .GroupBy(x => x.MentorId)
            .Select(g => new { MentorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MentorId, x => x.Count, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
