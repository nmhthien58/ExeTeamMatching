using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;

namespace Team.Repositories.TopicInterests;

public sealed class TopicInterestRepository(TeamDbContext db) : ITopicInterestRepository
{
    public Task<TopicInterest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<TopicInterest>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TopicInterest?> GetOpenInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct) =>
        db.Set<TopicInterest>()
            .FirstOrDefaultAsync(t => t.TeamId == teamId && t.AdvisorKind == advisorKind && t.AdvisorId == advisorId && t.Status == "OPEN", ct);

    public Task<TopicInterest?> GetLatestInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct) =>
        db.Set<TopicInterest>()
            .Where(t => t.TeamId == teamId && t.AdvisorKind == advisorKind && t.AdvisorId == advisorId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TopicInterest>> ListByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct) =>
        await db.Set<TopicInterest>()
            .AsNoTracking()
            .Where(t => t.AdvisorKind == advisorKind && t.AdvisorId == advisorId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TopicInterest>> ListByTeamIdAsync(Guid teamId, string? advisorKind, CancellationToken ct)
    {
        var query = db.Set<TopicInterest>()
            .AsNoTracking()
            .Where(t => t.TeamId == teamId);

        if (!string.IsNullOrEmpty(advisorKind))
        {
            query = query.Where(t => t.AdvisorKind == advisorKind);
        }

        return await query.OrderBy(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TopicInterest>> ListOpenByTeamIdAsync(Guid teamId, string advisorKind, CancellationToken ct) =>
        await db.Set<TopicInterest>()
            .Where(t => t.TeamId == teamId && t.AdvisorKind == advisorKind && t.Status == "OPEN")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public Task<int> CountOpenByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct) =>
        db.Set<TopicInterest>()
            .CountAsync(t => t.AdvisorKind == advisorKind && t.AdvisorId == advisorId && t.Status == "OPEN", ct);

    public Task<int> CountOpenByTeamAsync(Guid teamId, string advisorKind, Guid? excludeAdvisorId, CancellationToken ct)
    {
        var query = db.Set<TopicInterest>()
            .Where(t => t.TeamId == teamId && t.AdvisorKind == advisorKind && t.Status == "OPEN");

        if (excludeAdvisorId.HasValue)
        {
            query = query.Where(t => t.AdvisorId != excludeAdvisorId.Value);
        }

        return query.CountAsync(ct);
    }

    public async Task<bool> HasActiveAssignmentAsync(Guid teamId, string advisorKind, CancellationToken ct)
    {
        if (advisorKind == "LECTURER")
        {
            return await db.Set<LecturerAssignment>().AnyAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);
        }

        return await db.Set<MentorAssignment>().AnyAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);
    }

    public Task<int> CountActiveAssignmentsByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct)
    {
        if (advisorKind == "LECTURER")
        {
            return db.Set<LecturerAssignment>().CountAsync(a => a.LecturerId == advisorId && a.Status == "ASSIGNED", ct);
        }

        return db.Set<MentorAssignment>().CountAsync(a => a.MentorId == advisorId && a.Status == "ASSIGNED", ct);
    }

    public async Task<bool> WasLecturerEXE101InstructorAsync(Guid teamId, Guid lecturerId, CancellationToken ct)
    {
        var team = await db.Set<TeamEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null || team.CourseCode != "EXE201")
        {
            return false;
        }

        if (team.SourceTeamId is { } sourceTeamId)
        {
            var isSourceInstructor = await db.Set<LecturerAssignment>()
                .AsNoTracking()
                .AnyAsync(a => a.TeamId == sourceTeamId && a.LecturerId == lecturerId && a.Status == "ASSIGNED", ct);
            if (isSourceInstructor) return true;
        }

        if (team.ProjectId is { } projectId)
        {
            var exe101TeamIds = await db.Set<TeamEntity>()
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.CourseCode == "EXE101")
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (exe101TeamIds.Count > 0)
            {
                return await db.Set<LecturerAssignment>()
                    .AsNoTracking()
                    .AnyAsync(a => exe101TeamIds.Contains(a.TeamId) && a.LecturerId == lecturerId && a.Status == "ASSIGNED", ct);
            }
        }

        return false;
    }

    public async Task<int?> GetLecturerPreferenceRankAsync(Guid teamId, Guid lecturerId, CancellationToken ct)
    {
        var pref = await db.Set<LecturerPreference>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TeamId == teamId && p.LecturerId == lecturerId, ct);
        return pref?.Rank;
    }

    public async Task<IReadOnlyList<Guid>> ListTeamIdsInSemesterAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var query = db.Set<TeamEntity>().AsNoTracking().Where(t => t.SemesterId == semesterId);
        if (!string.IsNullOrEmpty(courseCode))
        {
            query = query.Where(t => t.CourseCode == courseCode);
        }
        return await query.Select(t => t.Id).ToListAsync(ct);
    }

    public void Add(TopicInterest interest) => db.Add(interest);

    public void AddOutboxMessage<T>(string topic, string eventType, string aggregateType, Guid aggregateId, T payload) =>
        db.AddOutboxMessage(topic, eventType, aggregateType, aggregateId, payload);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
