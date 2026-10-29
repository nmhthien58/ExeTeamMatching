using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;

namespace Team.Repositories.LecturerAssignments;

public sealed class LecturerAssignmentRepository(TeamDbContext db) : ILecturerAssignmentRepository
{
    public Task<LecturerAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct) =>
        db.Set<LecturerAssignment>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);

    public Task<List<LecturerAssignment>> ListActiveByLecturerIdAsync(Guid lecturerId, CancellationToken ct) =>
        db.Set<LecturerAssignment>().AsNoTracking()
            .Where(a => a.LecturerId == lecturerId && a.Status == "ASSIGNED")
            .ToListAsync(ct);
}
