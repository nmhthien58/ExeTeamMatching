using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;

namespace Team.Repositories.LecturerPreferences;

public sealed class LecturerPreferenceRepository(TeamDbContext db) : ILecturerPreferenceRepository
{
    public async Task<IReadOnlyList<LecturerPreference>> ListByTeamIdAsync(Guid teamId, CancellationToken ct) =>
        await db.Set<LecturerPreference>()
            .AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .OrderBy(p => p.Rank)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LecturerPreference>> ListByLecturerIdAsync(Guid lecturerId, CancellationToken ct) =>
        await db.Set<LecturerPreference>()
            .AsNoTracking()
            .Where(p => p.LecturerId == lecturerId)
            .OrderBy(p => p.Rank)
            .ToListAsync(ct);

    public async Task<bool> HasAssignedInstructorAsync(Guid teamId, CancellationToken ct) =>
        await db.Set<LecturerAssignment>()
            .AnyAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);

    public async Task<IReadOnlyList<TopicInterest>> ListOpenLecturerInterestsByTeamIdAsync(Guid teamId, CancellationToken ct) =>
        await db.Set<TopicInterest>()
            .AsNoTracking()
            .Where(t => t.TeamId == teamId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> IsLecturerInterestedAsync(Guid teamId, Guid lecturerId, CancellationToken ct) =>
        await db.Set<TopicInterest>()
            .AnyAsync(t => t.TeamId == teamId && t.AdvisorId == lecturerId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER", ct);

    // Cùng điều kiện với AdvisorMatchingRepository.CountLecturerLoadsAsync: nhóm đã giải tán không chiếm chỗ của giảng viên.
    public async Task<Dictionary<Guid, int>> CountActiveAssignmentsAsync(Guid semesterId, string courseCode, IReadOnlyList<Guid> lecturerIds, CancellationToken ct)
    {
        if (lecturerIds.Count == 0) return [];

        return await (from assignment in db.Set<LecturerAssignment>().AsNoTracking()
                join team in db.Set<TeamEntity>().AsNoTracking() on assignment.TeamId equals team.Id
                where lecturerIds.Contains(assignment.LecturerId) && assignment.Status == "ASSIGNED"
                    && team.SemesterId == semesterId && team.CourseCode == courseCode && team.Status != "DISBANDED"
                group assignment by assignment.LecturerId into groupByLecturer
                select new { LecturerId = groupByLecturer.Key, Count = groupByLecturer.Count() })
            .ToDictionaryAsync(x => x.LecturerId, x => x.Count, ct);
    }

    public async Task<HashSet<Guid>> GetInterestedTeamIdsAsync(Guid lecturerId, IEnumerable<Guid> teamIds, CancellationToken ct)
    {
        var idList = teamIds.Distinct().ToList();
        if (idList.Count == 0) return [];

        var matched = await db.Set<TopicInterest>()
            .AsNoTracking()
            .Where(t => idList.Contains(t.TeamId) && t.AdvisorId == lecturerId && t.Status == "OPEN" && t.AdvisorKind == "LECTURER")
            .Select(t => t.TeamId)
            .ToListAsync(ct);

        return matched.ToHashSet();
    }

    public async Task<LecturerPreferencePreviousInstructor?> GetPreviousInstructorAsync(Guid teamId, CancellationToken ct)
    {
        var team = await db.Set<TeamEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null || team.CourseCode != "EXE201")
        {
            return null;
        }

        if (team.SourceTeamId is { } sourceTeamId)
        {
            var sourceAssignment = await db.Set<LecturerAssignment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TeamId == sourceTeamId && a.Status == "ASSIGNED", ct);
            if (sourceAssignment is not null)
            {
                return new LecturerPreferencePreviousInstructor(sourceAssignment.LecturerId, sourceAssignment.LecturerName);
            }
        }

        if (team.ProjectId is { } projectId)
        {
            var exe101TeamId = await db.Set<TeamEntity>()
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.CourseCode == "EXE101")
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (exe101TeamId != Guid.Empty)
            {
                var assignment = await db.Set<LecturerAssignment>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TeamId == exe101TeamId && a.Status == "ASSIGNED", ct);
                if (assignment is not null)
                {
                    return new LecturerPreferencePreviousInstructor(assignment.LecturerId, assignment.LecturerName);
                }
            }
        }

        return null;
    }

    public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Assignment, eventType, "Team", teamId, payload);

    public async Task ReplacePreferencesAsync(
        Guid teamId,
        IReadOnlyList<LecturerPreference> preferences,
        CancellationToken ct)
    {
        var existing = await db.Set<LecturerPreference>().Where(p => p.TeamId == teamId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            db.Set<LecturerPreference>().RemoveRange(existing);
        }

        if (preferences.Count > 0)
        {
            await db.Set<LecturerPreference>().AddRangeAsync(preferences, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
