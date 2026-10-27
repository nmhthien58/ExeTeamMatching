using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;

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
}
