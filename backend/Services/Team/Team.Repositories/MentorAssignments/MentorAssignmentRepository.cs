using BuildingBlocks.Outbox;
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

    public async Task<Dictionary<Guid, int>> CountActiveTeamsByMentorIdsAsync(IReadOnlyList<Guid> mentorIds, Guid? semesterId, CancellationToken ct)
    {
        if (mentorIds.Count == 0) return [];

        var query = db.Set<MentorAssignment>()
            .AsNoTracking()
            .Where(a => mentorIds.Contains(a.MentorId) && a.Status == "ASSIGNED");

        if (semesterId.HasValue)
        {
            query = query.Where(a => db.Set<TeamEntity>().Any(t => t.Id == a.TeamId && t.SemesterId == semesterId.Value));
        }

        return await query
            .GroupBy(a => a.MentorId)
            .Select(g => new { MentorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MentorId, x => x.Count, ct);
    }

    public void AddOutboxMessage<TPayload>(string topic, string eventType, string aggregateType, Guid aggregateId, TPayload payload) =>
        db.AddOutboxMessage(topic, eventType, aggregateType, aggregateId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
